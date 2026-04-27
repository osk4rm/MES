# 0003. JWT Bearer authentication & interim RBAC via `IsTenantAdmin`

- Status: Accepted
- Date: 2026‑04‑23
- Deciders: core team

## Context

AsistOff MES needs authentication and coarse authorization for the SPA‑plus‑API shape. Two questions:

1. **Where do we keep the authenticated session?** Options: JWT Bearer in `Authorization` header, cookie session, BFF (Backend‑For‑Frontend) with HttpOnly cookies.
2. **How do we model authorization?** Options: ASP.NET Identity with roles, a custom `Roles/Permissions/UserRole` schema, or an external IdP (Keycloak, Auth0, Azure AD B2C).

The first release needs a minimal, correct model. Deferring correctness (e.g. hardcoded `permissions = ["users", "users.read", "configuration"]` returned for every user regardless of role, as was the case before this ADR) is unacceptable — it voids the RBAC contract at runtime.

## Decision

### Authentication

* Continue with **JWT Bearer tokens** in the `Authorization` header for the MVP.
  Pros: simple to implement, works well with the existing Vue SPA, matches the `AddJwtBearer` ASP.NET pipeline.
  Cons: tokens live in browser storage by default — **XSS exposure**. Documented as a known limitation.
* **Follow‑up (not in MVP)**: migrate to a BFF / HttpOnly cookie pattern with refresh‑token rotation. Tracked separately.

### Tenant claims

The token issued by `SignInRequestHandler` carries the following claims:

- `tenant_id` — `Guid` (authoritative tenant identifier, read back into the ambient `ITenantContext`)
- `tenant_name` — `Tenant.Name` (real, from DB)
- `tenant_active` — `Tenant.IsActive` (`"true"` / `"false"`)
- `tenant_display_name` — optional
- `permissions` — multi‑valued claim (see below)
- `email`, `sub` (user id), `role` (`tenant_admin` | `user`)

Sign‑in is rejected when:
* The user doesn't exist.
* The password verification fails.
* The referenced tenant doesn't exist (data corruption).
* The referenced tenant is inactive (`Tenant.IsActive == false`).

In every failure case the handler throws `AuthenticationException` (mapped to HTTP 401) — never `ValidationException` (400), which has different semantics.

### Authorization (interim)

Until a proper RBAC schema (`Role`, `Permission`, `UserRole`) is introduced, permissions are derived deterministically from `User.IsTenantAdmin`:

* `IsTenantAdmin == true` → `users*, configuration*, tenant.admin`
* otherwise → `users.read, configuration.read`

This is a **stopgap**. It removes the hardcoded permissions that ignored the caller's identity, but it is coarser than the final model.

### Authorization (target)

A future ADR will introduce:
* `Role`, `Permission`, `RolePermission`, `UserRole` tables (scoped by `TenantId` where appropriate).
* A claims builder that materialises `permissions` from the user's roles at sign‑in time.
* Declarative `[RequirePermission("configuration.products.write")]` attributes powered by an `AuthorizationBehavior` MediatR pipeline step.

## Consequences

**Positive**

- Sign‑in no longer hands out identical permissions to every user.
- Inactive tenants cannot sign in — a hard business rule.
- `AuthenticationException` gives operators a clean 401 signal in logs / API responses.

**Negative**

- Binary admin/non‑admin model is too coarse for real MES role differentiation (operator, shift leader, quality, planner, admin). The full RBAC is a near‑term follow‑up, not a future research item.
- JWTs in browser storage remain an XSS risk until the BFF migration ships.

## Alternatives considered

- **Keycloak / Auth0** — too heavy for MVP; would add operational surface and a vendor dependency before we have customers. Re‑evaluated when we hit the first enterprise deal.
- **ASP.NET Identity with EF roles** — reasonable but couples us to the default schema and doesn't model tenant‑scoped permissions cleanly.
