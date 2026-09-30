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
| Frontend UX review | — | Web | done | #314, #380, #382, #383, #389, #392 | audit + prioritised findings (`docs/ux-review/findings.md`); 3 fix slices + nav/state follow-ups (PRs #318, #381, #384, #385, #390, #393) |
| MES feature verification (recipes + Production Order flow) | BOM / Receptura, Production Order | Production, Web | done | #319, #386, #388 | friction list + rework proposal; order-ready recipes V1 with release checklist + version compare (PRs #322, #387, #391) |
| MES benchmark vs top-tier systems (recipes + Production Order) | BOM / Receptura, Production Order | Production, Web | done | #320 | `docs/benchmark-recipes-orders.md` vs Opcenter / SAP ME / FactoryTalk / Critical / AVEVA (PR #321) |
| Operator panel (dedicated shopfloor view) | Operator | Web | done | #335, #336 | shift-queue read-model + API and touch-friendly `OperatorPanelView` (PRs #338, #342) |
| Demo data seed script | — | Ops | done | — | `scripts/seed/seed-demo-data.ps1` — one-off API-driven seed of a running stack (master data, recipes/routing/BOM, orders, lots, confirmations, losses, SPC, kanban, telemetry, maintenance) |
| End-user manual (whole application) | — | Docs | done | #343, #345, #359 | 23-topic manual under `docs/manual/` (PRs #344, #347, #362) |
| Technical specification | — | Docs | done | #346, #352 | end-to-end spec under `docs/spec/` (PRs #349, #356) |

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
| Operator skill qualification matrix + gating | Operator | Configuration, Production | done | #397 | depends on Skills + Operators + Operator confirmations (RW / PW); qualification matrix + dispatch/confirmation gating (PR #402) |

## Production engineering

| Capability | Glossary | Module | Status | Work item | Notes |
|---|---|---|---|---|---|
| Recipes | BOM / Receptura | Production | done | — | `Recipe` |
| Recipe versions + release | — | Production | done | #388 | `RecipeVersion`; release flow + preflight checklist & version compare (PR #391) |
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
| Gantt scheduler (Harmonogram) | Operation / Routing | Production | done | #304, #305, #306 | depends on Operations / routing + Work-center calendar / shifts + Production Order; Gantt read-model + reschedule/leveling API + Harmonogram view; day/shift dispatch board renamed (PRs #310, #312, #317) |
| Production Order hold / suspend + resume | Production Order | Production | done | #398 | depends on Production Order; OnHold status + hold/resume transitions with reason code; confirmations + RW/PW movements blocked while held (PR #401); cf. benchmark O4/G-03 |

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
| OEE ideal cycle time from routing master data | Performance | Production | done | #399 | depends on Operations / routing + OEE; RunTimePerUnitSeconds → Performance instead of caller-supplied idealCycleTimeSeconds (PR #400) |

## Security hardening

Rows from the 2026-09-26 security audit. Every row now has a GitHub issue;
all but two are merged. Severity is recorded in `Notes`. #307 (workflow
isolation) and #363 (SHA pinning) are `ai:blocked` hence `proposed`.

| Capability | Glossary | Module | Status | Work item | Notes |
|---|---|---|---|---|---|
| Agent CI/CD workflow isolation | — | CI/CD | proposed | #307 | **Critical**; `ai-swarm.yml` uses `pull_request_target` + runs PR-controlled setup action with secrets in scope |
| Build-context secret exclusion | — | Ops | done | #308 | **High**; `.env` / signing key excluded from image layers via root + web `.dockerignore` (PR #309) |
| Sort-field whitelist enforcement (dynamic LINQ) | — | Shared | done | #311 | **High**; whitelist enforced on browse `sort` fields (PR #313) |
| Attachment object-level authorization | — | Attachments | done | #315 | **High**; `attachments.read` + owner-module scope enforced on download/list/delete (PR #316) |
| Rate-limit client-IP hardening | — | Gateway | done | #323 | **High**; trusted-proxy `RemoteIpAddress` instead of raw left-most XFF (PR #326) |
| Anonymous tenant lookup minimization | — | Multitenancy | done | #324 | **Medium**; minimal public projection, no ContactEmail/Settings (PR #327) |
| Module authorization policy wiring | — | Shared | done | #329 | **Medium**; `AddAuth` registers module `Policies` (PR #330) |
| Authentication-disable guard scope | — | Shared, Auth | done | #332 | **Medium**; bypass narrowed to explicit Development use, fail-closed elsewhere (PR #334) |
| SPA security headers + CSP | — | Web | done | #340 | **Medium**; nginx CSP / DENY / nosniff / Referrer-Policy (PR #341) |
| Attachment upload limits + quota + malware scan | — | Attachments | done | #348 | **Medium**; edge size limit, per-tenant quota, scan hook (PR #350) |
| Dev seed credential out of shipped image | — | Ops | done | #358, #364 | **Medium**; no hardcoded credential in published image (PRs #361, #368) |
| Swarm container privilege + socket | — | Ops | done | #357 | **Medium**; non-root swarm, no host socket/auth mounts (PR #360) |
| GitHub Actions SHA pinning | — | CI/CD | proposed | #363 | **Medium**; pin third-party actions to commit SHAs |
| Host header / HSTS / dev CORS hardening | — | Gateway | done | #369 | **Medium**; host validation, always-on HSTS, locked-down dev CORS (PR #371) |
| Attachment upload attribution | — | Attachments | done | #354 | **Low**; `UploadedByUserId` from current user (PR #355) |
| Sensitive read projections | — | Production | done | #372 | **Low**; audit `Payload` + OPC UA `LastError` redacted for read-only role (PR #374) |
| Attachment storage durability | — | Ops, Attachments | done | #373 | **Low**; content-root-relative path on durable volume (PR #375) |
| `.env` ignore coverage | — | Ops | done | #365 | **Low**; `.env.local` / `.env.production` / `.env.*.local` ignored (PR #367) |
| Global fallback authorization policy | — | Shared | done | #351 | **Low**; `RequireAuthenticatedUser` fallback (PR #353) |
| Frontend CSRF token + error-text sink | — | Web | done | #376 | **Low**; double-submit CSRF on cookie writes, text-only toasts (PR #379) |
| Seq authentication | — | Ops | done | #377 | **Low**; Seq authenticated, dev bound to localhost (PR #378) |
| Legacy dependency modernization | — | Shared, Users, Multitenancy | done | #366 | **Low**; EOL AspNetCore 2.x shims + Swashbuckle 6.x replaced (PR #370) |

_Last reconciled: 2026-09-30 — no changes: #307 + #363 stay `proposed` (`ai:blocked`); no open PRs; all code entities verified against tracker rows, nothing missing. Previously 2026-09-29 — all three `gap` rows closed out:_ operator skill matrix (#397, PR #402), Production Order hold/resume (#398, PR #401) and OEE ideal cycle time from routing (#399, PR #400) are `done`; #307 + #363 stay `proposed` (`ai:blocked`). No open PRs; no other status changes; no new code capabilities missing from the tracker. Previously 2026-09-28 — #308 (build-context secret exclusion, PR #309) is `done`: security audit rows now 20 `done`; #307 + #363 stay `proposed` (`ai:blocked`)._
