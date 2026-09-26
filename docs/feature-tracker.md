# MES Feature Tracker

Canonical, persistent map of what AsistOff MES can do, what is proposed, and
what is missing. It exists so agents do **not** rescan the whole repository on
every run.

## Contract (agents, read this)

- **mes-researcher** — reads this file **first** and is its only **appender**: it
  adds capabilities that are missing entirely as new `gap` rows, and never
  modifies, reorders, or deletes existing rows. It does not create GitHub
  issues.
- **mes-analyst** — reads this file **first**. Each run it either adopts the
  oldest unlabeled open issue, or takes the first `gap` row whose dependencies
  are `done`, and turns it into an `ai:implement`-labeled issue. It does not
  edit this file.
- **mes-tracker** — the single writer for statuses and work items. Reconciles
  this file with GitHub (issues / PRs) and the codebase, and publishes the
  update via a PR on branch `ai/tracker-sync`.
- **mes-implementer / mes-reviewer** — do **not** edit this file; keep PRs
  minimal. The tracker is reconciled out of band.
- Work items live in GitHub; this file stores only their identifiers.

## Status legend

| Status | Meaning |
|---|---|
| `done` | Implemented and merged. |
| `partial` | Exists but incomplete versus the glossary definition. |
| `proposed` | An open GitHub issue exists. |
| `in-progress` | An open PR implements it. |
| `gap` | Missing; no work item yet. |

## Dependencies

A row that needs another capability first names it in the `Notes` column,
prefixed with `depends on` (e.g. `depends on #80`, `depends on Lot / Serial`).
`mes-analyst` will not pick a `gap` row until every named dependency is `done`.

## Platform / cross-cutting

| Capability | Glossary | Module | Status | Work item | Notes |
|---|---|---|---|---|---|
| Multi-tenancy (tenant isolation) | Tenant | Multitenancy | done | — | ADR-0002; `ISaasy` + global query filter |
| Authentication / JWT + RBAC | — | Users, Auth | done | #208, #209, #210 | RBAC schema + role-derived sign-in (PRs #217, #219); roles mgmt UI (PR #224) |
| Polymorphic attachments | — | Attachments | done | — | `Attachment` |
| JWT hardening (audience, key strength, revocation) | — | Users, Auth | done | #227, #232, #280 | ValidateAudience + key-entropy floor + refresh rotation/revocation (PRs #230, #236, #283) |
| Attachment upload hardening | — | Attachments | done | #228, #234, #281 | MIME/extension allowlist, sniffed type, safe download disposition (PRs #229, #237, #284, #288) |
| Write-endpoint authorization (default-deny + full RBAC) | — | Users, Auth | done | #231, #233 | `RequirePermission` on all writes per ADR-0003 (PRs #238, #243) |
| Abuse protection (rate limiting, security headers, signup gating) | — | Gateway | done | #235 | per-IP throttle, HSTS/CSP/nosniff, minimal anonymous DTO (PRs #239, #240) |
| BFF cookie auth + refresh flow | — | Gateway, Web | done | #241, #242 | httpOnly cookies replace localStorage JWT, refresh rotation + redirect (PRs #244, #256) |
| Audit trail (actor + history) | — | Shared | done | #245, #246 | CreatedBy/ModifiedBy + append-only history (PRs #247, #248) |
| Health readiness split | — | Gateway | done | #249 | Npgsql readiness probe, `/health/live` vs `/health/ready` (PR #250) |
| Correlation ID end-to-end | — | Gateway, Web | done | #251 | X-Correlation-ID echo, LogContext, axios header, traceId in errors (PR #254) |
| OpenTelemetry metrics + traces | OEE | Gateway, Production | done | #252, #253 | OTLP/Prometheus export, business meters, OEE latency histograms (PRs #255, #264) |
| Prod deploy + backup safety | — | Ops | done | #257 | gated migration job, nightly pg_dump, env separation (PR #261) |
| Transactional outbox for domain events | — | Shared | done | #258, #259, #260 | OutboxMessages + relay with retry, no ghost events (PRs #262, #267, #282) |
| Optimistic concurrency tokens | Production Order | Production | done | #263 | xmin rowversion + 409/retry on Production Order (PR #269) |
| Container + runtime hardening | — | Ops | done | #271 | non-root USER, pinned digests, limits, generated secrets (PR #275) |
| Committed Playwright smoke suite | — | Web, CI | done | #272 | login→orders→confirm→lots on scripts/e2e harness (PR #276) |
| Frontend resilience bundle | — | Web | done | #273 | axios timeout/retry/abort, error state + retry, guards, boundary (PR #278) |
| Frontend UX review | — | Web | gap | — | holistic usability pass over every view (navigation/IA, forms, empty/loading/error states, i18n, shopfloor ergonomics); produce a prioritised findings list, then fix |
| MES feature verification (recipes + Production Order flow) | BOM / Receptura, Production Order | Production, Web | gap | — | hands-on verification that features are usable and flexible enough; recipes (versions, routing, BOM, skills) and the Production Order lifecycle are known-weak UX and need rework — capture concrete friction, then redesign |
| MES benchmark vs top-tier systems (recipes + Production Order) | BOM / Receptura, Production Order | Production, Web | gap | — | compare our recipe + order flow against leading MES (Siemens Opcenter, SAP ME/DMC, Rockwell FactoryTalk, Critical Manufacturing, AVEVA MES); produce a concrete gap list and prioritised improvements |
| Operator panel (dedicated shopfloor view) | Operator | Web | gap | — | operator-facing panel to claim and handle assigned tasks: current-shift work, confirmations, scrap/downtime, Andon, next-up queue — minimal chrome, touch-friendly |
| Demo data seed script | — | Ops | done | — | `scripts/seed/seed-demo-data.ps1` — one-off API-driven seed of a running stack (master data, recipes/routing/BOM, orders, lots, confirmations, losses, SPC, kanban, telemetry, maintenance) |
| End-user manual (whole application) | — | Docs | gap | — | user-facing manual covering every module and workflow (getting started, master data, recipes/routing, production orders, shopfloor, analytics, admin); delivered as Markdown under `docs/manual/` |
| Technical specification | — | Docs | gap | — | end-to-end specification of the system: architecture, module boundaries, domain model, API surface, multitenancy, auth/RBAC, deployment; Markdown document |

## Configuration (master data)

| Capability | Glossary | Module | Status | Work item | Notes |
|---|---|---|---|---|---|
| Products | — | Configuration | done | — | `Product`, `ProductGroup`, `ProductPrice` |
| Units of measure | Unit of Measure | Configuration | done | — | `MeasureUnit`, `ProductMeasureUnit` |
| Warehouses | RW / PW | Configuration | done | — | `Warehouse`; movements + stock rows below |
| RW/PW warehouse movements (persisted) | RW / PW | Configuration | done | #199 | `StockMovement` persisted on confirmation (PR #203) |
| Stock on hand | — | Configuration | done | #200 | per product + warehouse; WarehousesView card (PR #205) |
| Material reservations for released orders | RW / PW | Configuration | done | #291 | depends on Stock on hand + Production Order; soft-allocate on release, relieve on RW confirmation (PR #295) |
| Departments | — | Configuration | done | — | `Department` |
| Machines / resources | Work Center | Configuration | done | #204 | `Machine` + calendar (#83); capacity + efficiency factor (PR #206) |
| Operators | Operator | Configuration | done | — | `Operator` (code / RFID) |
| Skills | — | Configuration | done | — | `Skill` |
| EAN / GTIN product identification | EAN / GTIN | Configuration | done | #166, #176, #212 | shopfloor scan lookup Code→Ean→Barcode; tenant-unique Ean (PRs #168, #178, #218) |
| Operator shift assignment (roster) | Shift | Configuration | done | #167, #177 | `OperatorShiftAssignment`; which operators work which shift; depends on Work-center calendar / shifts |

## Production engineering

| Capability | Glossary | Module | Status | Work item | Notes |
|---|---|---|---|---|---|
| Recipes | BOM / Receptura | Production | done | — | `Recipe` |
| Recipe versions + release | — | Production | done | — | `RecipeVersion`; release flow |
| Operations / routing | Operation / Routing | Production | done | — | `OperationNode`, `OperationDependency` |
| Operation templates | — | Production | done | — | `OperationTemplate` |
| Operation outputs | — | Production | done | — | `OperationOutput` |
| BOM items | BOM | Production | done | — | `BomItem` |
| Resource requirements | Work Center | Production | done | — | `ResourceRequirement` |

## Production execution

| Capability | Glossary | Module | Status | Work item | Notes |
|---|---|---|---|---|---|
| Production Order | Production Order | Production | done | #80, #129, #130 | Planned -> Released -> InProgress -> Completed -> Closed; totals from confirmations |
| Dispatch board (shift-aware) | Shift | Production | done | #189, #190, #201 | Released-orders board replacing schedule placeholder; uncovered-shift flags (PRs #192, #194, #202) |
| Operator confirmations (RW / PW) | Confirmation | Production | done | #129, #130, #131, #199, #221 | confirmations + complete/close + RW/PW preview & persist; produced/consumed lots (PRs #135, #137, #141, #203, #225) |
| Scrap capture | Quality | Production | done | #97 | reason-code linked |
| Downtime capture | Downtime | Production | done | #84 | reason-code linked |
| Reason codes | Reason code | Configuration | done | #81 | dictionary (PR #82); linked by scrap / downtime capture |
| Work-center calendar / shifts | Shift | Configuration | done | #83 | calendar + entries per machine; shifts dictionary (PR #105) |
| Lot / Serial tracking | Lot / Serial | Production | done | #85 | `Lot` registry (PR #93) |
| Genealogy / traceability | Genealogy | Production | done | #142, #143, #144, #207 | `LotGenealogyEdge` auto-derived on confirmation; upstream/downstream traceability + lot tree (PRs #148, #151, #152, #211) |
| Atomic confirmation fan-out | Confirmation | Production | done | #265 | single transaction across confirmation + movements + edges + order (PRs #286, #287) |
| Read-path performance (paging, no-tracking, batch fetch) | Shift | Production | done | #274 | server-side DispatchBoard filtering/Take, AsNoTracking, MaxPageSize caps (PRs #277, #279) |
| Shift handover logbook | Shift | Production | done | #292, #293 | depends on Work-center calendar / shifts + Operator confirmations (RW / PW); context API + persisted entries with notes (PRs #294, #302) |
| Gantt scheduler (Harmonogram) | Operation / Routing | Production | gap | — | depends on Operations / routing + Work-center calendar / shifts + Production Order; time-phased Gantt of operations across Work Centers (drag/resize/level); takes over the "Harmonogram" name — the current day/shift dispatch board stays but must be renamed |

## Analytics / integration

| Capability | Glossary | Module | Status | Work item | Notes |
|---|---|---|---|---|---|
| OEE | OEE | Production | done | #153, #154, #156, #180, #181, #182 | per-Work Center snapshot API + trend buckets + loss Pareto + dashboard; Quality/Availability/Performance factors (PRs #157, #163, #165, #184, #185, #186) |
| Andon | Andon | Production | done | #98 | signals for abnormal conditions (PR #104) |
| SPC | SPC | Production | done | #99, #187, #188, #195, #196 | characteristic dictionary + measurements with out-of-control evaluation; log + control chart; Western Electric rules 2-4 (PRs #103, #191, #193, #197, #198) |
| CMMS | CMMS | Configuration | done | #100, #172 | corrective work orders + board UI (PRs #102, #174) |
| Preventive maintenance plans | CMMS | Configuration | done | #297, #298, #299 | depends on CMMS; time/meter-based schedules + due state + auto-raise of work orders (PRs #300, #301, #303) |
| OPC UA / SCADA telemetry | OPC UA | Production | done | #114, #115, #116, #160, #161 | tag dictionary + readings; simulator + stale dashboard; connection registry + polling (PRs #118, #127, #132, #162, #164) |
| Kanban | Kanban | Production | done | #145, #146, #147 | `KanbanLoop` dictionary + card registry; pull transitions with WIP limits; board UI (PRs #149, #155, #158) |
| MTBF / MTTR reliability KPIs | MTBF / MTTR | Production | done | #170, #171, #213, #214, #220 | per-Work Center snapshot query + API + dashboard; trend + fleet comparison (PRs #173, #175, #215, #216, #222) |
| OEE/analytics index review | OEE | Production | done | #266 | (TenantId,MachineId,ReportedAt) confirmations index, tenant FK indexes (PR #270) |

## Security hardening

Gaps from the 2026-09-26 security audit. No GitHub issues exist for these yet;
`mes-analyst` can promote them. Severity is recorded in `Notes`; `Critical`/`High`
should be addressed before the `Medium`/`Low` items.

| Capability | Glossary | Module | Status | Work item | Notes |
|---|---|---|---|---|---|
| Agent CI/CD workflow isolation | — | CI/CD | gap | — | **Critical**; `ai-swarm.yml` uses `pull_request_target` + checks out `refs/pull/N/{merge,head}` then runs the PR-controlled local action `./.github/actions/setup-opencode` (and npm/dotnet on PR code) with `SWARM_PAT`/`OPENCODE_API_KEY`/`GITHUB_TOKEN` in scope — pwn-request / secret exfiltration |
| Build-context secret exclusion | — | Ops | gap | — | **High**; `Dockerfile` `COPY . .` + `.dockerignore` missing `.env` bakes `POSTGRES_PASSWORD` / `auth:IssuerSigningKey` into image layers and BuildKit cache |
| Sort-field whitelist enforcement (dynamic LINQ) | — | Shared | gap | — | **High**; `QueryableExtensions.Sort` passes the unvalidated `sort` field to `System.Linq.Dynamic.Core` `OrderBy(string)`; `SortableValidator` is a closed generic over `ISortable` and never runs for concrete browse requests — expression injection (info disclosure / DoS) |
| Attachment object-level authorization | — | Attachments | gap | — | **High**; download/list/delete enforce tenant scope only; any authenticated tenant user can read/delete any attachment and `attachments.read` is never enforced |
| Rate-limit client-IP hardening | — | Gateway | gap | — | **High**; throttle partitions on the raw left-most `X-Forwarded-For`, spoofable to defeat the only anti-credential-stuffing control; use `Connection.RemoteIpAddress` after trusted-proxy config |
| Anonymous tenant lookup minimization | — | Multitenancy | gap | — | **Medium**; `GET /api/tenants/{id}` returns `ContactEmail` + `Settings` to anonymous callers, contradicting the minimal-public-projection contract |
| Module authorization policy wiring | — | Shared | gap | — | **Medium**; `AddAuth` is called without the module list so module `Policies` register nothing and are dead code |
| Authentication-disable guard scope | — | Shared, Auth | gap | — | **Medium**; `auth:AuthenticationDisabled` registers an evaluator that succeeds for all authz checks in any non-Production env |
| SPA security headers + CSP | — | Web | gap | — | **Medium**; nginx emits no CSP / `X-Frame-Options` / nosniff / `Referrer-Policy`; backend headers cover `/api` only |
| Attachment upload limits + quota + malware scan | — | Attachments | gap | — | **Medium**; 100 MB request buffered vs 10 MiB app cap, no per-tenant quota/rate limit and no AV scanning |
| Dev seed credential out of shipped image | — | Ops | gap | — | **Medium**; hardcoded `Passw0rd!` in `appsettings.Development.json` ships in the published image and the compose override runs Development |
| Swarm container privilege + socket | — | Ops | gap | — | **Medium**; swarm runs as root with the host Docker socket and host opencode auth dirs bind-mounted |
| GitHub Actions SHA pinning | — | CI/CD | gap | — | **Medium**; third-party actions pinned to mutable major tags, not commit SHAs |
| Host header / HSTS / dev CORS hardening | — | Gateway | gap | — | **Medium**; `AllowedHosts: "*"`, HSTS only when the request already arrives HTTPS, Development reflects any origin with credentials |
| Attachment upload attribution | — | Attachments | gap | — | **Low**; `UploadedByUserId` is never populated, relying on generic `CreatedBy` |
| Sensitive read projections | — | Production | gap | — | **Low**; full audit-event `Payload` JSON and raw OPC UA `LastError` visible to the read-only role |
| Attachment storage durability | — | Ops, Attachments | gap | — | **Low**; storage root resolves against process CWD and no persistent volume is mounted in compose |
| `.env` ignore coverage | — | Ops | gap | — | **Low**; `.gitignore` pattern `*.env` misses `.env.local` / `.env.production` |
| Global fallback authorization policy | — | Shared | gap | — | **Low**; no fallback policy, so non-`ApiController` controllers (`ErrorsController`) are anonymous by default |
| Frontend CSRF token + error-text sink | — | Web | gap | — | **Low**; no CSRF token on cookie writes; server error text rendered in toasts (safe today, XSS sink if `v-html` is added) |
| Seq authentication | — | Ops | gap | — | **Low**; Seq runs unauthenticated and is host-exposed in dev |
| Legacy dependency modernization | — | Shared, Users, Multitenancy | gap | — | **Low**; EOL `Microsoft.AspNetCore.*` 2.3.9 runtime packages and stale `Swashbuckle.AspNetCore` 6.6.2 |

_Last reconciled: 2026-09-26 — no `gap`, `proposed` or `in-progress` rows remain. #291 (material reservations, PR #295), #292/#293 (handover logbook, PRs #294, #302), #272/#276 (Playwright smoke suite, PR #276) and preventive maintenance (#297–#299, PRs #300/#301/#303) all merged to `done`. New `gap`: Gantt scheduler ("Harmonogram"), and the current day/shift dispatch board must be renamed so it no longer owns that label. #88/#89 are fixes with no capability rows. Security audit (2026-09-26) added a "Security hardening" section with 22 `gap` rows — 1 Critical, 4 High, 9 Medium, 8 Low — no GitHub issues yet._
