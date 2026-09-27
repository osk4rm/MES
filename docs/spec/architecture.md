# Architecture

How AsistOff MES is put together: a modular monolith with one deployable,
one database, and strict per-module layering. See
[ADR-0001](../adr/0001-modular-monolith.md) for the decision and
[Module boundaries](modules.md) for what each module owns.

## Shape of the system

```text
                    +------------------- Vue 3 SPA -------------------+
                    |              AsistOff.MES.Web                   |
                    +-------------------------------------------------+
                                              | HTTPS / JSON
                                              v
 +---------------- Gateway (single process, single deployable) ------+
 | AsistOff.MES.Gateway: Program.cs + ModuleLoader + DependencyInjection |
 |                                                                     |
 |  Multitenancy | Users | Configuration | Production | Attachments   |
 |  (own projects per layer, loaded as IModule plugins)               |
 +---------------------------------------------------------------------+
                                              |
                                              v
                              PostgreSQL (one shared database,
                              shared schema, TenantId per row)
```

- **Single deployable:** `AsistOff.MES.Gateway` is the only host process.
  `Program.cs` builds the host, registers every module, and serves the API.
- **Single database:** one shared PostgreSQL database and one shared schema.
  Tenant isolation is a code contract (`TenantId` + query filter), described
  in [Multitenancy](multitenancy.md), not a physical separation.
- **Module assemblies:** each business module ships its own projects
  (`*.Core`, `*.Application`, `*.Infrastructure`, `*.Api`, plus
  `*.Contracts` where another assembly needs its markers). Modules never
  reference another module's `Core`/`Application`/`Infrastructure` project;
  cross-module communication runs through MediatR requests, in-process
  domain events (transactional outbox relay), and shared contracts
  (`AsistOff.MES.Multitenancy.Contracts`).

## Layer responsibilities

Every business module uses the same four-layer split:

| Layer | Project suffix | Contains | Depends on |
|---|---|---|---|
| Domain | `*.Core` | Entities, enums, repository interfaces | `Shared.Abstractions`, `Multitenancy.Contracts` (markers only) |
| Application | `*.Application` | MediatR requests/handlers, validators, pipeline behaviors, `DependencyInjection` (`AddApplication`) | `*.Core` |
| Infrastructure | `*.Infrastructure` | EF Core `IEntityConfigurator` configurations, repository implementations, EF `DbContext` usage, `DependencyInjection` (`Add*Infrastructure`) | `*.Core`, `*.Application` |
| Presentation | `*.Api` | The `IModule` implementation plus ASP.NET Core controllers | `*.Application`, `*.Infrastructure` |

Cross-cutting code lives in two shared projects:

| Project | Responsibility |
|---|---|
| `AsistOff.MES.Shared.Abstractions` | Markers and contracts: `ISaasy`, `IEntity`, `IAuditable`, `RequirePermissionAttribute`, `AuthorizationAllowlist`, typed exceptions, `IModule` |
| `AsistOff.MES.Shared.Infrastructure` | EF Core `DefaultContext` (query filter, interceptors), MediatR behaviors (`ValidationBehavior`, `AuthorizationBehavior`), auth (`AddAuth`, JWT bearer, cookies), health probes, correlation IDs, observability, outbox relay |

## Gateway wiring (`AsistOff.MES.Gateway/Program.cs`)

Startup order as built:

1. Serilog bootstrap logger, then `builder.Host.ConfigureModules()`.
2. Configuration layering: `appsettings.*` < User Secrets < environment
   variables < command line, so `postgres__connectionString` and similar
   `__`-separated overrides win without editing secrets.
3. `ContainerSecretsValidator.Validate` — Production refuses to boot with a
   missing or weak `POSTGRES_PASSWORD` / `auth:IssuerSigningKey`.
4. `ModuleLoader.LoadModules()` discovers every `IModule` implementation by
   scanning `AsistOff.MES.*.dll` in the binary directory
   (`ModuleLoader.LoadApplicationAssemblies`, ordered by module name);
   `LoadAssemblies()` feeds MediatR (`RegisterServicesFromAssembly`) so all
   handlers are registered in-process.
5. `AddMesHealthChecks`, `AddExceptionHandling`, trusted-proxy
   `ForwardedHeaders` setup (default-deny; `AbuseProtectionPolicy`
   partitions on `Connection.RemoteIpAddress` only), OpenTelemetry
   (`AddMesObservability`), abuse protection (`AddAbuseProtection`), CORS
   (`DefaultPolicy`; Development may reflect origins, non-Development
   requires `cors:allowedOrigins`).
6. `AddPresentation` (controllers, Swagger, `CustomProblemDetailsFactory`)
   then `AddInfrastructure(configuration, assemblies, environment, modules)`
   — the module list is forwarded so every `IModule.Policies` entry becomes
   an MVC authorization policy — then `AddMultitenancy`, then each
   module's `Register(services, configuration)`.
7. Middleware order: `UseForwardedHeaders` first, then
   `CorrelationIdMiddleware`, `TenantTraceContextMiddleware`,
   `SecurityHeadersMiddleware`, Serilog request logging, Swagger (Development
   only), `UseExceptionHandler`, each module's `Use(app)`, the `BootRunner`
   migration/seeder gate (`Boot:ApplyMigrations` / `Boot:RunSeeders`,
   default off in Production; `--migrate-only` job mode for compose),
   `UseHttpsRedirection`, `UseCors`, `UseRateLimiter` (anonymous bootstrap
   endpoints only), `UseAuthentication`, `UseAuthorization`,
   `UseMesObservability` (`/metrics` only when enabled), anonymous health
   probes (`/health/live`, `/health/ready`, `/health`), then
   `MapControllers`.

## Module plugin contract (`IModule`)

`AsistOff.MES.Shared.Abstractions.Modules.IModule` exposes `Name`, `Path`,
`Policies`, `Register`, and `Use`. As built:

| Module | `Name` / `Path` | `Policies` | `Register` calls |
|---|---|---|---|
| Multitenancy | `Tenants` / tenant routes | none | `AddMultitenancy` (Gateway `Program.cs`) |
| Users | `Users` / `users` | none (empty) | `AddApplication` + `AddInfrastructure` |
| Configuration | `Configuration` / `configuration` | `configuration` | `AddApplication` + `AddConfigurationInfrastructure` |
| Production | `Production` / `production` | `production` | `AddProductionApplication` + `AddProductionInfrastructure` |
| Attachments | `Attachments` / `attachments` | `attachments` | `AddAttachmentsApplication` + `AddAttachmentsInfrastructure` |

All `Use(app)` implementations are currently no-ops; cross-cutting
middleware lives in the Gateway pipeline above, not in modules.

## Communication rules

- **Inside a module:** controller → MediatR request → handler → repository →
  EF Core. Handlers never write manual `TenantId == currentTenant` predicates;
  the global query filter in `DefaultContext` applies isolation.
- **Across modules:** only MediatR requests, domain events via the
  transactional outbox (`OutboxMessages` + `OutboxRelayService`, so no event
  is published without its transaction committing), and shared contracts.
  There are no module-to-module project references.
- **Async behavior:** every async method carries the `Async` suffix and takes
  a `CancellationToken`. Failures use typed exceptions from
  `AsistOff.MES.Shared.Abstractions.Exceptions` (`NotFoundException` → 404,
  `ValidationException` → 400, `AuthenticationException` → 401,
  `UnauthorizedAccessException` → 401, `ForbiddenException` → 403),
  formatted by the global exception handler.
