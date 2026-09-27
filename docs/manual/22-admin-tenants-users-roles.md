# Tenants, users and roles

Tenants, users and roles are the back-office foundation: the
tenant is your company workspace, users are the people who log
into it, and roles decide what each user may change. Signup and
role assignment are covered in [Getting started](01-getting-started.md);
this chapter is the administrator reference.

## Tenants

Tenants self-register — no invitation is needed — and there are no
authenticated tenant-management endpoints on the HTTP surface.

| Action | API | Notes |
|--------|-----|-------|
| Sign up | `POST /api/tenants` (anonymous, `201`) | Creates the tenant row and its first admin user; per-IP rate-limited at the gateway |
| Look up | `GET /api/tenants/{id}` (anonymous, `200`) | Public single-tenant lookup for the pre-auth surface |

Both return only the minimal public projection — id, name and
active status. Contact e-mail, display name, settings and secrets
are never returned.

Validation: the tenant name must not be empty (max 200
characters), the contact e-mail must be valid, and the password
and confirmation must match. Failures return `400` naming the
field.

## Users and authentication

There is no users CRUD controller: user provisioning happens
through tenant signup above, and sessions travel as `httpOnly`
`Secure` `SameSite=Lax` cookies (`mes_access` / `mes_refresh`).
The browser stores nothing in `localStorage`; the cookies are the
session. The `Authorization: Bearer` header keeps working during
the transition.

| Action | API | Notes |
|--------|-----|-------|
| Sign in | `POST /api/auth/sign-in` (anonymous) | E-mail plus password; per-IP rate-limited; sets both cookies and returns an empty token body plus user claims |
| Refresh | `POST /api/auth/refresh` (anonymous) | Token from the body when supplied, otherwise from the refresh cookie; rotates the single-use opaque token |
| Sign out | `POST /api/auth/sign-out` (authenticated, `204`) | Revokes the supplied token, otherwise all active tokens; clears both cookies |

Security behavior worth knowing:

- Bad credentials, an unknown user, a missing tenant row or an
  inactive tenant all return `401` — never `400` — without saying
  which half failed.
- Refresh reuses are treated as leaks: the whole token family is
  revoked and the replay returns `401`.
- Cookie writes additionally require a same-host Origin
  (next to `SameSite=Lax`); cross-origin writes return `403`.

## Roles and permissions

Permissions are short codes attached to the session at sign-in.
Reads never need a permission beyond being signed in; writes are
gated per module and rejected with `403`. The interim model
derives permissions from the admin flag: tenant admins hold
`users`, `configuration`, `production` and `attachments` (read
and write) plus `tenant.admin`; plain users hold the four reads.

Role management itself needs the `tenant.admin` permission on
every call. Open `/settings/roles` (other users are redirected to
the dashboard with an "access denied" message):

| Action | API | Notes |
|--------|-----|-------|
| List roles | `GET /api/roles` (`200`) | — |
| Role detail | `GET /api/roles/{id}` (`200`) | Permissions plus members |
| Create | `POST /api/roles` (`201`) | — |
| Rename | `PUT /api/roles/{id}` (`204`) | Route/body ID mismatch returns `400` |
| Set permissions | `PUT /api/roles/{id}/permissions` (`204`) | Replaces the whole permission set; mismatch returns `400` |
| Assign member | `POST /api/roles/{id}/members` (`204`) | Body role id must match the route, else `400` |
| Unassign member | `DELETE /api/roles/{id}/members/{userId}` (`204`) | — |
| Permission catalog | `GET /api/permissions` (`200`) | Fixed catalog to pick from |

Procedure for onboarding an operator clerk:

1. Confirm the tenant signup created their login (or create the
   role first when a new job function needs one).
2. Create or open the role and set its permissions from the
   catalog — for example `production.read` for a viewer,
   `production.write` for a line lead.
3. Assign the user as a member; their next sign-in carries the
   new permissions.
4. Verify by signing in as that user: allowed writes succeed,
   gated ones return `403`.

## Tenant isolation

Every record belongs to exactly one tenant and tenants cannot see
each other — the same global filter that scopes orders and lots
scopes users, roles and their claims. The tenant id in the
session comes from the back-office token claims, never from
caller input, and anonymous endpoints return no tenant-scoped
rows.

## Error cases

| Situation | Result |
|-----------|--------|
| Wrong e-mail or password, unknown user, inactive tenant | `401` |
| Missing or invalid tenant on a tenant request | `401` |
| Signed in without the required permission (writes, role management) | `403` |
| Cross-origin cookie write | `403` |
| Signup or role validation failure | `400` with the field named |
| Route/body ID mismatch | `400` |
| Unknown role or user id | `404` |
| Concurrent lifecycle write (stale row version) | `409` |

## Next steps

- First login flow: [Getting started](01-getting-started.md).
- What each permission unlocks per module: the Procedures in
  every chapter state their required permission.
- Who changed what afterwards: [Attachments and audit trail](23-attachments-audit.md).
- Session and tenant failure contract: [Health, correlation and observability](24-observability-health.md).
