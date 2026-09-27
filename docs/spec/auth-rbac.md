# Authentication and RBAC

How callers prove who they are and what they may do. See
[ADR-0003](../adr/0003-auth-jwt-and-rbac.md) for the decision and
[Multitenancy](multitenancy.md) for the tenant-isolation half.

## Request markers: tenant versus anonymous

Every MediatR request implements exactly one marker from
`AsistOff.MES.Multitenancy.Contracts/Interfaces` — there is no implicit
anonymous:

| Marker | Meaning | Enforced by |
|---|---|---|
| `ITenantRequest` / `ITenantRequest<TResponse>` | Requires an authenticated tenant (the default). | `TenantValidationBehavior`: no ambient tenant → `UnauthorizedAccessException` (401). |
| `IAllowAnonymousRequest` | Explicit opt-out; justified in the PR. | Skips tenant validation; tenant binding, if any, comes from stored data, never caller input. |

Anonymous requests as built (the complete set):

| Request | Why anonymous |
|---|---|
| `SignInRequest` (Users) | Pre-authentication credential check; no token exists yet. |
| `CreateTenantCommand` (Multitenancy) | Tenant self-provisioning; creates the tenant and its first admin. |
| `GetTenantQuery` (Multitenancy) | Public single-tenant lookup; minimal projection only. |
| `RefreshTokenRequest` (Users) | Session rotation; tenant binding comes from the stored token row. |
| Health probes (`/health/live`, `/health/ready`, `/health`) | Orchestrator checks with no user or tenant; not MediatR requests, mapped `AllowAnonymous` in `Program.cs`. |

## Sign-in and claims

`SignInRequestHandler` (Users) authenticates by e-mail + password hash
against `UsersRepository.GetForAuthenticationAsync`, then mints the session:

- **Rejected with `AuthenticationException` (401, never 400):** unknown user,
  bad password, missing tenant row, or `Tenant.IsActive == false`.
- **Token claims:** `tenant_id` (authoritative `Guid`, read back into the
  ambient `ITenantContext`), `tenant_name` (real `Tenant.Name`),
  `tenant_active` (`"true"`/`"false"`), optional `tenant_display_name`,
  multi-valued `permissions`, plus `email`, `sub` (user id), and `role`
  (`tenant_admin` | `user`).
- **Transport:** sessions travel as httpOnly `Secure` `SameSite=Lax` cookies
  (`mes_access` / `mes_refresh`); response bodies carry no usable token
  strings. The `Authorization: Bearer` header keeps working during the
  transition. Refresh mints a 256-bit opaque token, persists only its SHA-256
  hash (`RefreshToken`: `TokenHash`, `ExpiresAtUtc`, `FamilyId`), revokes the
  presented token on each rotation, and revokes the whole family on reuse
  (leak replay returns 401).

JWT hardening (enforced by `AuthOptionsValidator` at startup and the bearer
pipeline at runtime): the signing key (`auth:IssuerSigningKey`) must decode
to at least 32 bytes in every environment and Production additionally
rejects `ValidateIssuerSigningKey=false`; audience validation defaults on
and Production requires a configured audience, so tokens with a wrong or
missing `aud` are rejected with 401.

## Interim permission model

Until the full RBAC schema ships, permissions are derived deterministically
from `User.IsTenantAdmin` in
`SignInRequestHandler.ResolvePermissionsAsync` (and the refresh handler's
mirror), with constants centralized in `Users.Core/Rbac/RbacDefaults` — no
permission string literals elsewhere:

| Caller | Permissions |
|---|---|
| `IsTenantAdmin == true` | `users`, `users.read`, `users.write`, `configuration`, `configuration.read`, `configuration.write`, `production`, `production.read`, `production.write`, `attachments`, `attachments.read`, `attachments.write`, `tenant.admin` |
| otherwise (`user` role) | `users.read`, `configuration.read`, `production.read`, `attachments.read` |

The target model (`Role`, `Permission`, `RolePermission`, `UserRole` tables
scoped by `TenantId`, claims materialized from roles at sign-in) already
exists as data and management APIs (`RolesController`,
`PermissionsController`); the interim derivation above stays the sign-in
behavior until the follow-up ADR switches the claims builder over.

## Default-deny enforcement

`AuthorizationBehavior` (Shared Infrastructure pipeline) resolves in order:

1. Requests carrying `RequirePermissionAttribute` must have every declared
   permission in the caller's token, else `ForbiddenException` (403).
2. Requests on the documented `AuthorizationAllowlist` pass (anonymous
   bootstrap, session maintenance, all browse/get reads for any
   authenticated user; attachments list/download are explicitly not listed
   — they require `attachments.read` plus owner-module scope).
3. Requests from legacy pass-through assemblies pass (the set is empty since
   the full-RBAC slice: everything uncovered fails closed).
4. Anything else is rejected with `ForbiddenException` instead of executing,
   so a new write added without a permission declaration fails closed at
   runtime and in the `AuthorizationCoverageTests` suite.

`Program.cs` forwards the discovered `IModule.Policies` list into
`AddAuth`, registering one MVC authorization policy per module
(`configuration`, `production`, `attachments`); controllers combine the
tenant gate, the JWT bearer/cookie authentication, and these policies before
any handler runs. The `auth:AuthenticationDisabled` evaluator that bypasses
all checks exists only outside Production and only as a documented dev/test
aid.

## Failure-code contract

| Situation | Exception | HTTP |
|---|---|---|
| Bad credentials, inactive tenant, rotated-token reuse | `AuthenticationException` | 401 |
| Missing or invalid tenant on a tenant-scoped request | `UnauthorizedAccessException` | 401 |
| Authenticated but missing a required permission | `ForbiddenException` | 403 |
| Unknown id | `NotFoundException` | 404 |
| Business-rule or input violation | `ValidationException` | 400 |
| Concurrent lifecycle write (stale `xmin`) | concurrency conflict | 409 |

## Rules for contributors

1. New requests carry exactly one of `ITenantRequest` / `IAllowAnonymousRequest`.
2. New writes carry `RequirePermissionAttribute` with `RbacDefaults`
   constants; new reads either carry one or are added to
   `AuthorizationAllowlist` with a justification comment.
3. Never grant anonymous access to tenant-scoped rows: anonymous handlers take
   no tenant id from the caller.
