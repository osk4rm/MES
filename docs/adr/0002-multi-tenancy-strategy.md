# 0002. Shared database with `TenantId` + EF Core global query filter

- Status: Accepted
- Date: 2026‑04‑23
- Deciders: core team

## Context

AsistOff MES is a multi‑tenant SaaS: the same deployed instance serves many customer organizations ("tenants"). A hard requirement is that **one tenant can never read or write another tenant's data**, including by guessing primary keys.

Three textbook isolation strategies exist:

1. **Database per tenant** — strongest isolation; operationally expensive (one more DB to migrate per customer); overkill for SMB plans.
2. **Schema per tenant** (same DB, different schemas) — medium isolation; schema explosion; migrations run N times.
3. **Shared schema with `TenantId` column on every tenant row** — cheapest; isolation is a code contract, so the code must enforce it rigorously.

The product target mixes long‑tail SMB tenants (cost‑sensitive) with a few enterprise tenants (isolation‑sensitive).

## Decision

**Default**: shared database, shared schema, every tenant‑scoped entity has a `TenantId` column.

The isolation is enforced by **three layers of defense**:

1. **`ISaasy` marker interface** — every tenant‑scoped entity implements `ISaasy { Guid TenantId; }`. Enforced by convention, checked in code review.
2. **EF Core global query filter** — `DefaultContext.OnModelCreating` iterates the model and applies `HasQueryFilter(e => e.TenantId == CurrentTenantId)` to every `ISaasy` entity. The ambient tenant id is read from a `DbContext` instance property (`CurrentTenantId`), so EF Core parameterizes it per query. Any LINQ query — regardless of which repository calls it — is silently constrained to the caller's tenant.
3. **`SaasyEntityInterceptor`** (a `SaveChangesInterceptor`):
   * On `Added`: if `TenantId == Guid.Empty`, auto‑assigns from the ambient tenant; if set to a different tenant, throws.
   * On `Modified`: rejects any change to `TenantId` (entities cannot be re‑homed).
4. **`TenantValidationBehavior`** (MediatR pipeline) — blocks any request that is not `IAllowAnonymousRequest` when no tenant is present in the HTTP context, failing fast with 401 instead of silently returning empty result sets.

Callers that legitimately need to bypass the filter (e.g. pre‑authentication user lookup in `SignInRequestHandler`) use a narrow, explicitly named API (`IUsersRepository.GetForAuthenticationAsync`) that calls `IgnoreQueryFilters()`. Every such call site must be reviewed.

### For Enterprise plan (future)

When a customer requires physical isolation, we will add a **database‑per‑tenant** mode on top of the same code, using `DbContextFactory` with per‑tenant connection strings. The EF model and isolation logic remain unchanged.

### Row Level Security (RLS)

As defense‑in‑depth in production we plan to additionally enable PostgreSQL Row Level Security policies on tenant‑scoped tables. This protects against accidental `dotnet ef database` operations or ad‑hoc queries that bypass the EF layer.

## Consequences

**Positive**

- Cheap per‑tenant cost; shared migrations; linear scaling to thousands of tenants.
- Isolation enforced centrally — no per‑handler code required.
- Migration path to DB‑per‑tenant exists for premium tiers.

**Negative**

- Model cache is shared across tenants; the filter lambda must reference a **DbContext instance property** (not a captured closure) so EF parameterizes it correctly. Enforced by `DefaultContext.ApplyTenantQueryFilter`.
- Bypass APIs (`IgnoreQueryFilters`) are dangerous; must be reviewed case‑by‑case.
- Noisy‑neighbor risk on DB resources; mitigated by connection pooling + per‑tenant rate limiting (planned).

## Alternatives considered

- DB per tenant — too expensive at our scale; reserved for enterprise.
- Schema per tenant — migration hell; rejected.
- Keycloak/Auth0 tenant models — orthogonal; will revisit for identity (ADR‑0003).
