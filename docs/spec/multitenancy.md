# Multitenancy

How tenant isolation works: shared database, `TenantId` on every
tenant-scoped row, and three central enforcement layers. See
[ADR-0002](../adr/0002-multi-tenancy-strategy.md) for the decision; the
request/auth half of the contract is in
[Authentication and RBAC](auth-rbac.md).

## Model

- **Default:** one shared PostgreSQL database, one shared schema, every
  tenant-scoped entity carries a `TenantId` column. The only shared
  (non-tenant-scoped) entity is the `Tenant` registry itself
  (`AsistOff.MES.Multitenancy/Entity/Tenant.cs`).
- **Marker:** every tenant-scoped entity implements `ISaasy`
  (`AsistOff.MES.Multitenancy.Contracts`, `Guid TenantId`). Convention-checked
  in review; handlers must not add manual `TenantId == currentTenant`
  predicates because the query filter already applies them.
- **Future path:** a database-per-tenant mode for enterprise plans reuses the
  same model via per-tenant connection strings; PostgreSQL Row Level Security
  is planned as defense-in-depth. Neither is built yet.

## Enforcement layers

| # | Layer | Location | Rule |
|---|---|---|---|
| 1 | Global query filter | `DefaultContext.ApplyTenantQueryFilter` | `HasQueryFilter(e => e.TenantId == CurrentTenantId)` on every `ISaasy` entity. `CurrentTenantId` is a `DbContext` instance property so EF Core parameterizes it per query; with no ambient tenant it returns `Guid.Empty`, so tenant-scoped queries return zero rows by default. |
| 2 | Save interceptor | `SaasyEntityInterceptor` (registered in `DefaultContext.OnConfiguring`) | On `Added`: empty `TenantId` is backfilled from the ambient tenant, an explicit foreign `TenantId` throws `InvalidOperationException`; without an ambient tenant (migrations, startup) inserts without a `TenantId` throw. On `Modified`: any change to `TenantId` throws — rows cannot be re-homed. |
| 3 | Request gate | `TenantValidationBehavior` (MediatR pipeline, registered in `Multitenancy/DependencyInjection.cs`) | Any request that is not `IAllowAnonymousRequest` without an ambient tenant fails fast with `UnauthorizedAccessException` (HTTP 401) instead of silently returning empty sets. |

Supporting interceptors run in the same `SaveChanges` pipeline
(`DefaultContext.OnConfiguring` order): `AuditableEntityInterceptor`
(stamps `CreatedBy`/`ModifiedBy`, `CreatedAt`/`UpdatedAt`), then
`AuditHistoryInterceptor` (append-only history, validated by the tenant
guard in the same save), then `SaasyEntityInterceptor`, then
`PublishDomainEventsInterceptor` (outbox, so events commit with their
transaction).

## Ambient tenant plumbing

- `TenantContext` (in `AsistOff.MES.Multitenancy/Context`) implements
  `ITenantContext` / `ICurrentTenantAccessor` from the JWT `tenant_id` claim
  established at sign-in. `DefaultContext.CurrentTenantId` reads it through
  the accessor.
- Inactive tenants (`Tenant.IsActive == false`) cannot sign in
  (`SignInRequestHandler` throws `AuthenticationException`); a token carrying
  `tenant_active == false` is therefore never minted.

## The pre-auth bypass (and only bypasses)

The global filter hides tenant rows from any query without a tenant. Code
that must run before authentication uses narrow, explicitly named repository
methods that call `IgnoreQueryFilters()`; each is justified at the call site:

| Method | Why it bypasses |
|---|---|
| `UsersRepository.GetForAuthenticationAsync(email)` (and its tenant-pinned overload) | Pre-auth credential lookup: no ambient tenant exists yet, so the filter would hide the very row needed to authenticate. |
| `RefreshTokensRepository.GetByHashIgnoringQueryFiltersAsync(hash)` | Opaque refresh-token lookup: tenant binding comes from the stored row's `TenantId`, never from caller input; rotation then re-establishes the ambient tenant. |
| Anonymous `GetTenantQuery` path | Single-tenant public lookup on the provisioning surface; returns only the minimal projection (id, name, isActive), never contact e-mail, settings, or secrets. |

No handler, repository, or background service outside these three paths may
call `IgnoreQueryFilters`. Telemetry pollers and the simulator hold no
bypass: they run inside the ambient tenant and rely on the filter alone.
Any new bypass requires a maintainer review comment.

## Rules for contributors

1. New tenant-scoped entities implement `ISaasy` and get an EF configuration
   with at least a `(TenantId, Code)` unique constraint where a code exists.
2. Handlers express tenant requirements with `ITenantRequest` (default) or
   `IAllowAnonymousRequest` (explicit opt-out with PR justification) — see
   [Authentication and RBAC](auth-rbac.md). Never both, never neither.
3. Never write manual `TenantId` predicates in handlers and never call
   `IgnoreQueryFilters` outside the three justified paths above.
