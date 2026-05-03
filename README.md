# AsistOff MES

> **Manufacturing Execution System** — a multi‑tenant SaaS platform built as a modular monolith with a Vue 3 SPA and an ASP.NET Core 10 backend.

## At a glance

| Layer        | Technology                                            |
|--------------|-------------------------------------------------------|
| Backend      | .NET 10, ASP.NET Core, C#                              |
| Frontend     | Vue 3, TypeScript, Vite                               |
| Database     | PostgreSQL via Entity Framework Core 10                |
| Auth         | JWT Bearer tokens                                     |
| CQRS         | MediatR + `IRequestValidator<T>` pipeline behaviors   |
| Multi‑tenancy| EF Core global query filter + `SaasyEntityInterceptor`|
| State (FE)   | Pinia                                                 |
| UI Library   | PrimeVue 4                                            |
| i18n         | vue‑i18n                                              |

## Repository layout

```
AsistOff.MES.Gateway/               # ASP.NET entry point — composes all modules
AsistOff.MES.Shared.Abstractions/   # Interfaces & contracts used across modules
AsistOff.MES.Shared.Infrastructure/ # Cross‑cutting infrastructure (EF Core, auth, messaging)
AsistOff.MES.Multitenancy(.*)/      # Tenant module (entity + contracts)
AsistOff.MES.Configuration.*/       # Configuration module (Products, Warehouses, Departments, …)
AsistOff.MES.Users.*/               # Users module (authentication, user CRUD)
AsistOff.MES.Web/                   # Vue 3 SPA frontend
tests/                              # xUnit test projects
docs/                               # Architecture Decision Records, glossary
```

Each domain module follows the `*.Core` / `*.Application` / `*.Infrastructure` / `*.Api` split.

## Quick start (Docker Compose)

```bash
docker compose up --build
```

On boot the Gateway:

1. Applies all pending EF Core migrations for every registered `DbContext` (see
   `AsistOff.MES.Shared.Infrastructure.MigrationExtensions.ApplyAllPendingMigrations`).
2. Runs every registered `ISeeder`. When the environment is `Development` and
   `Seed:Enabled` is `true`, `DevTenantSeeder` provisions the tenants configured under
   `Seed:Tenants` in `appsettings.Development.json`. The seeder is **idempotent** — it skips
   any tenant whose `Name` already exists — and uses the exact same `CreateTenantCommand`
   pipeline as public registration, so the tenant-admin user is also created through the
   regular `TenantCreatedEvent` listener.

Default development credentials (from `AsistOff.MES.Gateway/appsettings.Development.json`):

| Field     | Value              |
|-----------|--------------------|
| Tenant    | `dev`              |
| Email     | `admin@dev.local`  |
| Password  | `Passw0rd!`        |

Sign in with:

```bash
curl -X POST http://localhost:5080/api/auth/sign-in \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin@dev.local","password":"Passw0rd!"}'
```

Then use the returned `AccessToken` as a bearer token, or log in through the Vue SPA.

### Adding more dev tenants

Append entries to the `Seed:Tenants` array in `appsettings.Development.json`, or override via
environment variables, e.g.:

```bash
Seed__Tenants__1__Name=qa
Seed__Tenants__1__DisplayName=QA Tenant
Seed__Tenants__1__ContactEmail=admin@qa.local
Seed__Tenants__1__AdminPassword=Passw0rd!
```

### Self-service tenant registration (public)

The same flow is available for real registrations at `POST /api/tenants` (anonymous). See
`AsistOff.MES.Multitenancy/Controllers/TenantsController.cs`.

### Disabling the seeder

Set `Seed:Enabled=false` (or run the Gateway outside `Development`). `DevTenantSeeder`
short-circuits when either condition is false, so production images never auto-provision
tenants.

## Running the project

### Prerequisites

- .NET SDK 9.0
- Node.js 20+
- PostgreSQL 14+ (or use Docker)

### Backend

```bash
dotnet restore AsistOff.MES.sln
dotnet run --project AsistOff.MES.Gateway         # http://localhost:5080
```

Connection string is in `AsistOff.MES.Gateway/appsettings.*.json` (key: `Postgres:ConnectionString`). Migrations are applied automatically at startup.

### Frontend

```bash
cd AsistOff.MES.Web
npm ci
npm run dev                                        # http://localhost:5173
npm run build                                      # type‑check + production bundle
```

### Tests

```bash
dotnet test AsistOff.MES.sln                       # runs all test projects under tests/
```

### EF Core migrations

```bash
# Add a migration (to the main DbContext)
dotnet ef migrations add <Name> \
  --project AsistOff.MES.Shared.Infrastructure \
  --startup-project AsistOff.MES.Gateway \
  --context DefaultContext

# Remove the last unapplied migration
dotnet ef migrations remove \
  --project AsistOff.MES.Shared.Infrastructure \
  --startup-project AsistOff.MES.Gateway \
  --context DefaultContext
```

## Multi‑tenancy model

* Every tenant‑scoped entity implements `ISaasy` (has `TenantId`).
* A global EF Core **query filter** is applied automatically to every `ISaasy` entity in `DefaultContext.OnModelCreating` — no handler can accidentally read another tenant's data.
* `SaasyEntityInterceptor` (a `SaveChangesInterceptor`) auto‑assigns the ambient `TenantId` on insert and rejects any attempt to change it on update.
* Every MediatR request must either implement `ITenantRequest` (tenant is required) or `IAllowAnonymousRequest` (explicit opt‑out, e.g. sign‑in, tenant provisioning). The `TenantValidationBehavior` enforces this.

See [`docs/adr/0002-multi-tenancy-strategy.md`](docs/adr/0002-multi-tenancy-strategy.md).

## Adding a new feature

1. Add request (`IRequest<T>` or `IJblRequest<T>`) in `*.Application/Features/<Area>/<Name>/`.
2. Mark it with `ITenantRequest` **or** `IAllowAnonymousRequest` — no third option.
3. Add a `RequestHandler` and (if it needs business validation) a `RequestValidator`.
4. Add a controller endpoint in `*.Api/Controllers/`.
5. Add a test in `tests/AsistOff.MES.Shared.Tests/` (or a dedicated module test project).
6. Open a PR using the pull request template — tick the multi‑tenancy checklist.

## Contributing

* Conventional commits are encouraged (`feat:`, `fix:`, `chore:`, `docs:` …).
* The PR template enforces a multi‑tenancy checklist.
* See [`AGENT.md`](AGENT.md) for AI‑agent working agreements.
* For AI Copilot / Claude context see [`.github/copilot-instructions.md`](.github/copilot-instructions.md) and [`CLAUDE.md`](CLAUDE.md).

## Further reading

* [`docs/adr/`](docs/adr/) — Architecture Decision Records
* [`docs/glossary.md`](docs/glossary.md) — MES domain glossary (EN/PL)
* [`.github/instructions/`](.github/instructions/) — scoped instructions for specific areas (frontend, API, database, testing, architecture)
