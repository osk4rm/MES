# Endpoint catalog

Every HTTP endpoint the Gateway serves, per module, with its route, auth
requirement, and permission requirement. Controller and pipeline mechanics are
defined in [Architecture](architecture.md); the tenant/auth enforcement behind
the Auth column is defined in [Multitenancy](multitenancy.md) and
[Authentication and RBAC](auth-rbac.md).

## How to read the Auth column

HTTP authorization is deliberately thin: the base controller
(`Shared.Infrastructure/Controllers/ApiController`) carries `[ApiController]`
plus `[Authorize]`, and no controller or action in the system uses
`[Authorize(Policy = ...)]`. The real enforcement runs on the MediatR request
inside the pipeline (`TenantValidationBehavior`, then
`AuthorizationBehavior`):

| Auth value | Meaning |
|---|---|
| anonymous | Action carries `[AllowAnonymous]` and the request carries `IAllowAnonymousRequest`. No token, no tenant. Exactly four actions (plus the health probes, which are not MediatR requests — see [Observability](observability.md)). |
| signed-in | Any authenticated caller. The request is on the documented `AuthorizationAllowlist` (bootstrap, session maintenance, all Configuration/Production browse and get reads). |
| signed-in + `permission` | Any authenticated caller holding the named permission. The request carries `[RequirePermission(...)]` with a `RbacDefaults` constant; anything else fails closed with 403, including requests that declare nothing. Attachments list and download additionally require the owner-module scope permission (see Attachments below). |

MVC model-binding and validation failures never reach a handler: they are
formatted by `CustomProblemDetailsFactory` (Gateway `DependencyInjection`)
into the same ProblemDetails envelope described below.

Response-code conventions: browse returns `200` with a page envelope (see
Paging below); get returns `200`; create returns `201 CreatedAtAction`
(except attachment upload, which returns `200`, and in-place updates on
Production Orders and Andon signals, which return `200` with the updated
body); put and lifecycle transitions return `200` or `204` per table; delete
returns `204`. Route/body id mismatch returns `400`.

## Multitenancy: tenants (`api/tenants`)

Controller `TenantsController` (`AsistOff.MES.Multitenancy/Controllers`).
Both actions are anonymous; both return the minimal public projection
(`AnonymousTenantResponse`: id, name, active flag) — never contact e-mail,
settings, or secrets.

| Method and route | Auth | Notes |
|---|---|---|
| `POST api/tenants` (`CreateTenantCommand`) | anonymous | Self-provisioning: creates the Tenant row and its first admin user. Per-IP rate-limited at the Gateway. Returns `201`. |
| `GET api/tenants/{id}` (`GetTenantQuery`) | anonymous | Single-tenant public lookup. Returns `200`. |

There are no authenticated tenant-admin CRUD endpoints on the HTTP surface.

## Users: session, roles, permissions

No `UsersController` exists: user provisioning happens through tenant
self-provisioning above (first admin) and role membership below. The Users
HTTP surface is session plus role/permission management.

### Session (`api/auth`)

Controller `AuthenticationController` (`AsistOff.MES.Users.Api`).

| Method and route | Auth | Notes |
|---|---|---|
| `POST api/auth/sign-in` (`SignInRequest`) | anonymous | E-mail plus password. Sets httpOnly `Secure` `SameSite=Lax` cookies (`mes_access` / `mes_refresh`); usable token strings are blanked from the body. Rejects cross-origin writes with 403. Per-IP rate-limited. Returns `200`. |
| `POST api/auth/refresh` (`RefreshTokenRequest`) | anonymous | Accepts the refresh token from the body or the `mes_refresh` cookie; rotates the token family (reuse of a rotated token revokes the whole family with 401) and re-issues the cookie pair. Returns `200`. |
| `POST api/auth/sign-out` (`SignOutRequest`) | signed-in | Revokes the presented token, or all session tokens when no token is supplied. Clears both cookies. Returns `204`. |

There is no `GET` session/profile endpoint and no password-reset endpoint on
the HTTP surface.

### Roles and permissions (`api/roles`, `api/permissions`)

Controllers `RolesController`, `PermissionsController`. Unlike the
Configuration/Production reads, these management reads are not allowlisted:
every action below requires the `tenant.admin` permission (the request types
carry `[RequirePermission(RbacDefaults.TenantAdmin)]`).

| Method and route | Auth | Notes |
|---|---|---|
| `GET api/roles` (`BrowseRolesRequest`) | signed-in + `tenant.admin` | Role list. Returns `200`. |
| `GET api/roles/{id}` (`GetRoleRequest`) | signed-in + `tenant.admin` | Role detail with permissions and members. Returns `200`. |
| `POST api/roles` (`CreateRoleRequest`) | signed-in + `tenant.admin` | Returns `201`. |
| `PUT api/roles/{id}` (`UpdateRoleRequest`) | signed-in + `tenant.admin` | Route/body id mismatch returns `400`. Returns `204`. |
| `PUT api/roles/{id}/permissions` (`SetRolePermissionsRequest`) | signed-in + `tenant.admin` | Replaces the role's permission set. Returns `204`. |
| `POST api/roles/{id}/members` (`AssignUserToRoleRequest`) | signed-in + `tenant.admin` | Returns `204`. |
| `DELETE api/roles/{id}/members/{userId}` (`UnassignUserFromRoleRequest`) | signed-in + `tenant.admin` | Returns `204`. |
| `GET api/permissions` (`BrowsePermissionsRequest`) | signed-in + `tenant.admin` | Fixed catalog from `RbacDefaults`. Returns `200`. |

## Configuration: master data (`api/...`)

All controllers inherit the authenticated base with no action-level
overrides. Reads are allowlisted (any signed-in caller); creates, updates,
deletes, and state transitions carry
`[RequirePermission(RbacDefaults.ConfigurationWrite)]`. The canonical shape is
`GET /` browse (paged) → `GET /{id}` get → `POST /` create (`201`) →
`PUT /{id}` update (`204`) → `DELETE /{id}` delete (`204`); deviations are
noted per controller.

| Controller (route) | Endpoints | Auth | Notes |
|---|---|---|---|
| `ProductsController` (`api/products`) | Browse, `GET by-scan?value=`, Get `{id}`, Create, Update, Delete | reads signed-in; scan lookup signed-in; writes signed-in + `configuration.write` | Scan lookup (`GetProductByScanRequest`) resolves Code, then Ean, then barcode. |
| `ProductGroupsController` (`api/product-groups`) | Browse, Get, Create, Update, Delete | reads signed-in; writes signed-in + `configuration.write` | Product grouping dictionary. |
| `MeasureUnitsController` (`api/measure-units`) | Browse, Get, Create, Update, Delete | reads signed-in; writes signed-in + `configuration.write` | Unit-of-measure dictionary. |
| `WarehousesController` (`api/warehouses`) | Browse, Get `{id}`, Create, Update | reads signed-in; writes signed-in + `configuration.write` | No delete endpoint. |
| `StockMovementsController` (`api/stock-movements`) | `GET /` browse only | signed-in | Read-only RW/PW ledger; rows are written only by Production confirmations. |
| `StockOnHandController` (`api/stock-on-hand`) | `GET /` only | signed-in | Signed PW minus RW balances per product and warehouse. |
| `MaterialReservationsController` (`api/material-reservations`) | `GET /` browse (paged), `GET /{id}` | signed-in | Read-only; reservations are created on order release and relieved on RW confirmation. |
| `DepartmentsController` (`api/departments`) | Browse, Get, Create, Update, Delete | reads signed-in; writes signed-in + `configuration.write` | Organizational dictionary. |
| `MachinesController` (`api/machines`) | Browse, Get, Create, Update, Delete, `GET {id}/calendar`, `PUT {id}/calendar` | reads signed-in; writes signed-in + `configuration.write` | The machine is the Work Center. Calendar get returns empty entries (not 404) for a known Work Center without a calendar. |
| `OperatorsController` (`api/operators`) | Browse, Get, Create, Update, Delete | reads signed-in; writes signed-in + `configuration.write` | Shopfloor operators (distinct from back-office users). |
| `OperatorShiftAssignmentsController` (`api/operator-shift-assignments`) | Browse, Get, Create, Delete | reads signed-in; writes signed-in + `configuration.write` | No update endpoint; roster rows are replaced, not edited. |
| `ShiftsController` (`api/shifts`) | Browse, Get, Create, Update, Delete | reads signed-in; writes signed-in + `configuration.write` | Named working windows. |
| `SkillsController` (`api/skills`) | Browse, Get, Create, Update, Delete | reads signed-in; writes signed-in + `configuration.write` | Capability tags matched against operation requirements. |
| `ReasonCodesController` (`api/reason-codes`) | Browse, Get, Create, Update, Delete | reads signed-in; writes signed-in + `configuration.write` | Controlled vocabulary for scrap and downtime causes. |
| `MaintenancePlansController` (`api/maintenance-plans`) | Browse (paged), `GET due`, Get `{id}`, Create, Update, Delete, `POST evaluate-due`, `POST {id}/raise-now` | reads and due-list signed-in; evaluate and raise signed-in + `configuration.write` | `evaluate-due` merges body and query meter readings; `raise-now` returns the raised work order (`200`). |
| `MaintenanceWorkOrdersController` (`api/maintenance-work-orders`) | Browse, Get, Create, `POST {id}/start`, `POST {id}/complete`, `POST {id}/cancel` | reads signed-in; all writes signed-in + `configuration.write` | No update or delete endpoints; `complete` takes optional resolution notes. |

## Production: engineering and execution (`api/...`)

Same auth split as Configuration: reads are allowlisted (any signed-in
caller); creates, updates, deletes, and lifecycle transitions carry
`[RequirePermission(RbacDefaults.ProductionWrite)]`.

| Controller (route) | Endpoints | Auth | Notes |
|---|---|---|---|
| `RecipesController` (`api/recipes`) | Browse (paged), Get, Create, Update, Delete | reads signed-in; writes signed-in + `production.write` | Recipe headers. |
| `RecipeVersionsController` (`api/recipe-versions`) | `GET /{id}`, `POST /`, `POST /clone`, `PUT /{id}/metadata`, `POST /{id}/release`, `DELETE /{id}` | read signed-in; all writes signed-in + `production.write` | Draft to Released lifecycle; only released versions are orderable. |
| `OperationsController` (`api`, flat sub-paths) | `POST operations`, `PUT operations/{id}`, `DELETE operations/{id}`, `POST recipe-versions/{versionId}/operations/reorder`, `PUT operations/{id}/dependencies`, `POST operations/{id}/bom-items`, `PUT bom-items/{id}`, `DELETE bom-items/{id}`, `POST operations/{id}/outputs`, `PUT outputs/{id}`, `DELETE outputs/{id}`, `POST operations/{id}/resources`, `PUT resources/{id}`, `DELETE resources/{id}` | signed-in + `production.write` (all actions mutate) | Routing graph, BOM lines, outputs, and resource requirements. Mutations without a body return `204`. |
| `OperationTemplatesController` (`api/operation-templates`) | Browse (paged), Get, Create, Update, Delete | reads signed-in; writes signed-in + `production.write` | Reusable operation blueprints. |
| `ProductionOrdersController` (`api/production-orders`) | Browse (paged), Get `{id}`, Create (`201`), Update (`200` with body), Delete (`204`), `POST {id}/release`, `POST {id}/complete`, `POST {id}/close`, `GET {id}/movements`, `GET {id}/history` | reads, movement preview, and history signed-in; writes signed-in + `production.write` | Planned to Released to InProgress to Completed to Closed. History synthesizes an audit browse (page 1, size 50). Concurrent lifecycle writes return 409. |
| `ProductionConfirmationsController` (`api/production-confirmations`) | Browse (paged), Get, Create (`201`), Delete (`204`), `GET {id}/movements` | reads signed-in; writes signed-in + `production.write` | No update endpoint; corrections are delete plus re-create. Posting runs the atomic fan-out (see [Domain model](domain-model.md)). |
| `LotsController` (`api/lots`) | Browse (paged), `GET by-code/{code}`, Get `{id}`, Create (`201`), Update, `POST {id}/status`, Delete | reads signed-in; writes signed-in + `production.write` | Status change takes the target `LotStatus` in the body. |
| `LotGenealogyController` (`api/lot-genealogy`) | Browse (paged), Get, `POST /` record (`201`), Delete, `GET upstream/{lotId}?maxDepth=`, `GET downstream/{lotId}?maxDepth=` | reads and both trace queries signed-in; writes signed-in + `production.write` | Same-lot self links are rejected. |
| `ScrapEventsController` (`api/scrap-events`) | Browse (paged), Get, Create, Update, Delete | reads signed-in; writes signed-in + `production.write` | Scrap quantities with reason-code links. |
| `DowntimeEventsController` (`api/downtime-events`) | Browse (paged), Get, Create (`201` start), `POST {id}/close` (`200`), Update, Delete | reads signed-in; writes signed-in + `production.write` | Open while `EndedAt` is null; close takes the optional end timestamp. |
| `AndonSignalsController` (`api/andon-signals`) | Browse (paged), Get, Create (`201` raise), `POST {id}/acknowledge` (`200`), `POST {id}/resolve` (`200`), Update (`200` with body), Delete | reads signed-in; writes signed-in + `production.write` | Active to Acknowledged to Resolved. |
| `KanbanController` (`api/kanban`) | `GET loops` (paged), `GET loops/{id}`, `POST loops` (`201`), `PUT loops/{id}`, `DELETE loops/{id}`, `GET loops/{loopId}/cards`, `GET loops/{loopId}/cards/{id}`, `POST loops/{loopId}/cards` (`201`), `DELETE loops/{loopId}/cards/{id}`, `POST cards/{id}/consume`, `POST cards/{id}/order`, `POST cards/{id}/replenish` | reads signed-in; writes signed-in + `production.write` | Card reads pin the loop server-side; a loop mismatch returns 404. |
| `ScheduleController` (`api/schedule`) | `GET dispatch?from&to`, `GET operator-queue?operatorCode&take=`, `GET gantt?from&to&machineId=`, `PUT gantt/segments/{id}` (`200`) | reads signed-in; reschedule signed-in + `production.write` | Operator queue `take` is clamped server-side; reschedule supports forced moves with a concurrency token. |
| `OeeController` (`api/oee`) | `GET /`, `GET /snapshot`, `GET /trend`, `GET /losses` (all `?machineId&fromUtc&toUtc`) | signed-in | Computed summaries, never stored rows. |
| `ReliabilityController` (`api/reliability`) | `GET snapshot`, `GET trend`, `GET fleet` (all `?machineId&fromUtc&toUtc`) | signed-in | MTBF/MTTR views per Work Center and fleet. |
| `TelemetryReadingsController` (`api/telemetry-readings`) | `GET /` browse, `GET /trend`, `GET /{id}`, `POST /` submit (`201`) | reads signed-in; submit signed-in + `production.write` | Append-only by design (no update or delete). Browse serves `text/csv` export (capped at 5000 rows) when the caller sends `Accept: text/csv`. |
| `MachineTelemetryTagsController` (`api/telemetry-tags`) | Browse (paged), `GET status`, Get, Create, Update, `POST {id}/toggle`, Delete | status and reads signed-in; writes signed-in + `production.write` | Tag dictionary per Work Center. |
| `OpcUaConnectionsController` (`api/opcua-connections`) | Browse (paged), `GET status`, Get, Create, Update, `POST {id}/toggle`, `POST {id}/test`, Delete | status, reads, and connection test signed-in; writes signed-in + `production.write` | The connection test validates shape only. |
| `SpcCharacteristicsController` (`api/spc-characteristics`) | Browse (paged), Get, Create, Update, Delete | reads signed-in; writes signed-in + `production.write` | Measured-characteristic dictionary. |
| `SpcMeasurementsController` (`api/spc-measurements`) | Browse (paged), `GET chart`, Get `{id}`, `POST /` record (`201`) | reads and chart signed-in; record signed-in + `production.write` | Append-only by design (no update or delete). |
| `ShiftHandoversController` (`api/shift-handovers`) | `GET context`, `POST /` create (`201`), `GET /` browse, `GET /{id}` | reads and context signed-in; create signed-in + `production.write` | Context takes discrete confirmation page parameters; browse returns its own page envelope. |
| `AuditEventsController` (`api/audit-events`) | `GET /` browse (paged) | signed-in | Append-only audit history in reverse time order. |

## Attachments (`api/attachments`)

Controller `AttachmentsController`
(`AsistOff.MES.Attachments.Api`). Not on the read allowlist: list and
download require `attachments.read` plus the owner-module scope permission,
upload and delete require `attachments.write`
(`AttachmentScopePolicy`). The edge size limit is 11 MiB (10 MiB application
cap plus a 1 MiB multipart margin); larger bodies are rejected with `400`
before reaching the handler.

| Method and route | Auth | Notes |
|---|---|---|
| `GET api/attachments?ownerType=&ownerId=` (`ListAttachmentsRequest`) | signed-in + `attachments.read` + owner scope | Polymorphic list per owner entity. Returns `200`. |
| `POST api/attachments` (`UploadAttachmentRequest`, multipart form) | signed-in + `attachments.write` | MIME allowlist plus sniffed-type verification; per-tenant quota applies. Returns `200` with the stored row. |
| `GET api/attachments/{id}/download` (`DownloadAttachmentRequest`) | signed-in + `attachments.read` + owner scope | Safe `attachment` disposition, sanitized content type, `X-Content-Type-Options: nosniff`. Returns the file. |
| `DELETE api/attachments/{id}` (`DeleteAttachmentRequest`) | signed-in + `attachments.write` | Returns `204`. |

## Error contract (ProblemDetails)

All failures — thrown typed exceptions (`GlobalExceptionHandler` in Shared
Infrastructure) and MVC binding/validation failures
(`CustomProblemDetailsFactory`) — serialize as `application/problem+json`
with the RFC 7807 fields (`title`, `status`, `detail`, plus `type` and
`instance`) and one system extension:

| Field | Source |
|---|---|
| `title`, `status` | Mapped from the exception type (table below). |
| `detail` | The exception message, except for unhandled failures, which return a generic message so internals never leak. Validation failures additionally carry the `errors` dictionary. |
| `traceId` | The effective `X-Correlation-ID` (see [Observability](observability.md)), so any error can be joined to its trace and logs. |

| Situation | Exception | HTTP |
|---|---|---|
| Input or business-rule violation (including bad sort fields and page validation) | `ValidationException` | 400 |
| Route/body id mismatch | MVC `BadRequest` via the controller guard | 400 |
| Bad credentials, inactive tenant, rotated-token reuse | `AuthenticationException` | 401 |
| Missing or invalid tenant on a tenant-scoped request | `UnauthorizedAccessException` | 401 |
| Authenticated but missing the required permission (including default-deny and the sign-in CSRF guard) | `ForbiddenException` | 403 |
| Unknown id (including Kanban card loop mismatch) | `NotFoundException` | 404 |
| Concurrent lifecycle write (stale `xmin`) or Gantt segment conflict (carries `conflictingSegmentIds`) | `ConcurrencyConflictException` / `GanttScheduleConflictException` | 409 |
| Repository failure | `RepositoryException` | 500 |
| Anything else | `Exception` | 500 |

## Paging, sorting, and filtering

Browse actions take their query from a `[FromQuery]` browse request and
return a page envelope (`PagedResponse<T>`: `Items`, `TotalCount`,
`TotalPages`); non-paged reads (stock, OEE, reliability, dispatch, Gantt,
traceability, telemetry trend, movement previews, shift-handover context)
return plain lists. The conventions, enforced by shared validators, are:

- **Paging:** `PageNumber` (default 1) and `PageSize` (default 10, capped per
  request by `MaxPageSize`, usually 100). Both must be present and positive
  when the request caps the size. The typeahead `Search` filter on products
  caps the page at 20 and orders by code.
- **Sorting:** repeatable `RawSort` query keys (`?RawSort=Name,desc`),
  `Field` or `Field,asc|desc` grammar, restricted to identifier characters
  and to the request's `SupportedSortFields` whitelist; anything else returns
  400. Multiple keys compose as ordered/then-by sorts.
- **Filtering:** optional per-request fields (`Name`, `Code`, `Search`,
  `IsActive`, date ranges, foreign keys such as `GroupId` or `MachineId`);
  semantics are substring or equality matches evaluated in the database via
  the shared `PageFilter` (`Where`, then `Sort`, then `Page`) composition.

Special cases: the shift-handover browse returns its own
`PagedShiftHandoversResponse` envelope; the shift-handover context and the
operator queue take discrete `take`/page integers instead of the shared page
request; the Production Order history endpoint hard-codes page 1 with size
50; the telemetry CSV export is capped at 5000 rows.
