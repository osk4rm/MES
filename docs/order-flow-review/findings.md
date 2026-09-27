# Order-flow verification — recipes + Production Order lifecycle (slice 1/2)

Parent issue: #386. Tracker gap: Platform → MES feature verification
(recipes + Production Order flow). **This slice is findings-only — no
production code was changed.** The redesign implementation lands in the
(2/2) follow-up, scoped at the end of this document as separable vertical
increments.

This slice is distinct from the Frontend UX review series (#380, #382,
#383), which audited navigation, forms, empty/loading/error states, i18n
and design-system compliance across every view. This slice tests
**functional fit**: can a planner create and release a recipe version with
routing, BOM, skills and resource requirements, dispatch a Production Order
through Planned → Released → InProgress → Completed → Closed, and can an
Operator confirm work with RW/PW movements, scrap, downtime and lot
traceability without workarounds.

Glossary terms are used as defined in `docs/glossary.md`: BOM / Receptura,
Production Order, Routing, Operation, Work Center, Confirmation, Operator,
Lot / Serial, Genealogy (Traceability), RW / PW, Reason code, Shift, Andon.

## Method

- Systematic static walkthrough of the recipe flow (`views/production/`
  `RecipesView.vue`, `RecipeDetailView.vue`,
  `components/production/RecipeVersionEditor.vue`, services
  `recipeService.ts`, `recipeVersionService.ts`,
  `operationTemplateService.ts`) and the Production Order flow
  (`ProductionOrdersView.vue`, `ProductionOrderDetailView.vue`,
  `ScheduleDispatchView.vue`, `OperatorPanelView.vue`, `ScrapView.vue`,
  `DowntimeView.vue`, `LotsView.vue`, `AndonView.vue`, services
  `productionOrderService.ts`, `productionConfirmationService.ts`,
  `confirmationLots.ts`, `lotGenealogyService.ts`,
  `scheduleService.ts`, `operatorQueueService.ts`), cross-checked against
  the backend controllers (`Production.Api/Controllers/`
  `RecipesController.cs`, `RecipeVersionsController.cs`,
  `OperationsController.cs`, `OperationTemplatesController.cs`,
  `ProductionOrdersController.cs`, `ProductionConfirmationsController.cs`,
  `LotGenealogyController.cs`, `LotsController.cs`, `ScrapEventsController.cs`,
  `DowntimeEventsController.cs`, `ScheduleController.cs`) and `src/i18n.ts`
  (default `pl`, fallback `en`).
- The seeded-stack click-through could not run in this environment (no
  Docker / app / database — CI owns integration, Playwright e2e is a later
  stage), so every friction item below carries **repro steps written for the
  seeded local stack** (`pwsh -File scripts/e2e/app.ps1 -Action start`,
  admin `admin@dev.local` / `Passw0rd!`). The reviewer re-walk protocol:
  switch the top-bar locale PL↔EN, visit the listed view, follow the steps,
  confirm the observation. Both locales were walked statically (every view
  below renders only `$t(...)` keys; namespaces `recipes`,
  `productionOrders`, `productionConfirmations`, `movements`,
  `scheduleDispatch`, `operatorPanel`, `scrap`, `downtime`, `lots`,
  `andon` exist in both locales).
- Severity: **blocker** (flow cannot be completed without a workaround),
  **major** (primary-flow barrier, data-integrity risk, or shopfloor
  stopper that has a workaround), **minor** (inconsistency / polish that
  slows the planner or operator down).
- Each item records: affected view + step, attempted task, observed
  behaviour versus expectation, repro steps, severity.

## Flow-visit log (both locales)

| # | Step | View file(s) | PL | EN |
|---|------|--------------|----|----|
| F-1 | Recipe list + create/edit/delete | `views/production/RecipesView.vue` | visited | visited |
| F-2 | Recipe detail, version picker, new-version, cleanup prompt | `views/production/RecipeDetailView.vue` | visited | visited |
| F-3 | Version editor: operations, dependencies, BOM, outputs, resources, release | `components/production/RecipeVersionEditor.vue` | visited | visited |
| F-4 | Operation templates dictionary | `views/configuration/OperationTemplatesView.vue` | visited | visited |
| F-5 | Skills dictionary | `views/configuration/SkillsView.vue` | visited | visited |
| F-6 | Order list + create/edit/release | `views/production/ProductionOrdersView.vue` | visited | visited |
| F-7 | Order detail: summary, RW/PW preview, confirmations, complete/close | `views/production/ProductionOrderDetailView.vue` | visited | visited |
| F-8 | Dispatch board (shifts + orders) | `views/production/ScheduleDispatchView.vue` | visited | visited |
| F-9 | Operator panel (queue, shift card, signals) | `views/production/OperatorPanelView.vue` | visited | visited |
| F-10 | Scrap registry | `views/production/ScrapView.vue` | visited | visited |
| F-11 | Downtime registry | `views/production/DowntimeView.vue` | visited | visited |
| F-12 | Lot registry + scan + genealogy tab | `views/production/LotsView.vue` | visited | visited |
| F-13 | Andon board | `views/production/AndonView.vue` | visited | visited |

## Findings — recipe flow

### R-1 — Recipe list hides order-readiness (major)

- **View / step:** `/production/recipes`, list table.
- **Attempted task:** As a planner, pick a recipe that can be used on a new
  Production Order (i.e. has a released version).
- **Observed vs expected:** The table shows only `code`, `name`, `isActive`.
  There is no current-version badge and no "no released version" warning, so
  a recipe with zero released versions looks identical to a released one.
  Expected: a version badge (`v3 · released` / `no released version`) per
  row. The detail view (`RecipeDetailView.vue`) already knows
  `currentVersionId` — the data is available, it is just not surfaced in
  the list.
- **Repro:** Seed the stack, create recipe `R-NEW` without releasing any
  version, open `/production/recipes` in PL, then in EN. Both rows look
  equally usable; only opening the detail reveals the missing release.
- **Severity:** major (planner discovers the dead end one click later, in
  the order form where non-released versions render disabled).

### R-2 — New-version creation is one-click with no options (major)

- **View / step:** `/production/recipes/:id`, "new version" action
  (`createVersion` in `RecipeDetailView.vue`).
- **Attempted task:** Create v4 from v3 with change notes and a validity
  window.
- **Observed vs expected:** One click clones the released version if one
  exists, otherwise creates a blank draft — no dialog, no source-version
  choice (e.g. clone the obsolete v2 instead of released v3), no
  `ChangeNotes` / `ValidFrom` / `ValidTo` input at creation time. Expected:
  a small creation dialog (source version + change notes + validity),
  because traceability of *why* a version exists matters for audits.
- **Repro:** Open any recipe with a released version, click "new version".
  A draft appears with no prompt. Repeat in EN — same behaviour.
- **Severity:** major (workaround exists — edit metadata afterwards — but
  the intent is never captured at creation).

### R-3 — Routing is edited as flat predecessor dropdowns, no graph (major)

- **View / step:** Recipe detail → version editor → dependencies tab
  (`RecipeVersionEditor.vue`).
- **Attempted task:** Build a 5-operation routing with a parallel branch and
  verify it is a valid DAG.
- **Observed vs expected:** Operations render as a flat sidebar ordered by
  `SortIndex`; dependencies are added per operation via a predecessor
  dropdown plus a raw list with a remove button. There is a node-strip
  (`dependency-map`) but no edge visualization, no cycle warning, and no
  topological validation in the UI — the backend `PUT
  /api/operations/{id}/dependencies` accepts the set per operation, so a
  cycle or a cross-version predecessor is only caught (if at all) late.
  Expected: a minimal graph hint (predecessor chips on each node, cycle
  alert) before release.
- **Repro:** Create a draft with operations A, B, C. Set A's predecessor to
  C and C's predecessor to A. No UI complaint. Release attempt behaviour is
  backend-defined; the UI gives no preflight signal.
- **Severity:** major (invalid routing is the most expensive recipe defect;
  it breaks every downstream order).

### R-4 — No recipe-level BOM summary (major)

- **View / step:** Recipe detail → version editor → BOM per operation.
- **Attempted task:** Answer "what material, in total, does one unit (or one
  batch) of this recipe consume?"
- **Observed vs expected:** `BomItem` is operation-level only, and the UI
  shows it per operation tab. There is no aggregated demand view (sum over
  operations, honouring `QuantityType` PerUnit/PerBatch/PerOperationRun and
  `ScrapPercentage`). Expected: a version-level "material demand" rollup so
  the planner can sanity-check the recipe and purchasing can see totals.
- **Repro:** Build a 3-operation draft with BOM items on each operation.
  There is no screen showing the combined demand. Check PL and EN.
- **Severity:** major (workaround: click every operation and add up by
  hand — error-prone and locale-independent drudgery).

### R-5 — Resource-requirement capability is a display string, not a skill link (major)

- **View / step:** Version editor → resources tab; `/configuration/skills`.
- **Attempted task:** Require skill ` spawacz-TIG` on an operation, then
  rename the skill.
- **Observed vs expected:** `ResourceRequirement.RequiredCapability` stores
  the human-readable skill display string, not the skill ID (confirmed in
  the business-features map and the editor). Renaming a skill silently
  orphans every requirement referencing it — no warning, no "skill no
  longer in dictionary" badge. Expected: store the skill ID and resolve the
  display name, or at minimum flag dangling capabilities.
- **Repro:** Create skill `S-1`, reference it from an operation, rename it
  to `S-1b`, reopen the operation. The requirement still shows the old
  string with no warning.
- **Severity:** major (silent data-integrity decay; scheduling/skill
  enforcement will inherit it).

### R-6 — Operation templates only prefill on manual add (major)

- **View / step:** Version editor → add operation; `/configuration/
  operation-templates`.
- **Attempted task:** Apply template `T-CUT` to an existing operation, or
  see that template `T-CUT` changed after the operation was created.
- **Observed vs expected:** Templates prefill code/name/type/timing when
  *adding* an operation. There is no "apply template to this operation",
  no bulk-instantiate, and no drift indicator when the template changes
  later. Expected: apply-to-existing + drift badge ("differs from template
  T-CUT rev N").
- **Repro:** Create template, add operation from it, edit the template,
  reopen the operation. Nothing signals the divergence.
- **Severity:** major (template library rots: planners stop trusting it).

### R-7 — Release has no preflight checklist UI (minor)

- **View / step:** Version editor → release (`releaseVersion`).
- **Attempted task:** Release a draft confidently.
- **Observed vs expected:** Release is a single button; the only
  UI-visible/ backend-enforced gate is "at least one operation". Nothing
  checks: outputs defined, BOM products still active, warehouses set,
  validity dates coherent, dependencies acyclic. Expected: a release
  checklist dialog (checks with pass/warn/fail) so release is a conscious
  sign-off.
- **Repro:** Create a draft with one bare operation (no BOM, no outputs),
  click release. It releases.
- **Severity:** minor (backend rule is satisfied; the gap is missing
  guidance, not missing enforcement).

### R-8 — No version diff (major)

- **View / step:** Recipe detail → version picker.
- **Attempted task:** Review what changed between v2 (obsolete) and v3
  (released) before cloning v4.
- **Observed vs expected:** Versions are selectable buttons with status
  badges, but opening two versions side by side or seeing an
  operations/BOM/resources diff is impossible — the planner must memorise
  v2 while looking at v3. Expected: a version-compare view (added/removed/
  changed operations, BOM deltas, timing deltas).
- **Repro:** Open a recipe with ≥2 versions, switch between them. No diff
  affordance in PL or EN.
- **Severity:** major (change review is an audit requirement in regulated
  manufacturing).

### R-9 — Lookup caps hide catalog items (minor)

- **View / step:** Recipe create/edit modal → primary-product autocomplete
  (`productService.browse({ pageNumber: 1, pageSize: 100 })`).
- **Attempted task:** Set the primary product in a catalog with >100
  products.
- **Observed vs expected:** The dropdown loads the first 100 products with
  no search-as-you-type against the server — items beyond the cap are
  unreachable. Same pattern (100/200 caps) recurs in order lookups.
  Expected: server-side search in the autocomplete.
- **Repro:** Seed >100 products, open the recipe modal, type a code that
  sorts past the first page. It never appears.
- **Severity:** minor (blocks only large catalogs, but then it blocks hard).

## Findings — Production Order flow

### O-1 — Order creation lets product, recipe and version disagree (major)

- **View / step:** `/production/orders` → create modal
  (`ProductionOrdersView.vue`).
- **Attempted task:** Create order `ZO-100` for product `P-A` using recipe
  `R-B` (whose primary product is `P-X`).
- **Observed vs expected:** Product, recipe and recipe-version are three
  independent dropdowns. `PrimaryProductId` is informational and never
  filters or warns: the form accepts product `P-A` + recipe `R-B` (primary
  product `P-X`) + any released version without a mismatch hint. Only the
  version dropdown disables non-released versions (good). Expected: recipe
  list filtered/sorted by selected product, plus a visible mismatch warning
  when they disagree.
- **Repro:** Select product A, recipe B (primary product X), a released
  version, save. The order is created with no warning, in both locales.
- **Severity:** major (wrong-recipe orders are the costliest order defect;
  the data to prevent it — `PrimaryProductId` — already exists).

### O-2 — Dispatch board dispatches nothing (blocker)

- **View / step:** `/schedule/dispatch` (`ScheduleDispatchView.vue`).
- **Attempted task:** Dispatch released order `ZO-100` to shift S-2 on
  machine M-5 as the next job.
- **Observed vs expected:** The board renders shift cards (code, name,
  time, headcount, uncovered badge) plus an orders table (code, due date,
  overdue badge, priority, remaining, status) with row-click navigating to
  order detail. There is **no dispatch action**: no assign-to-shift, no
  assign-machine/operator, no sequencing. "Dispatch" is read + navigate
  away. Expected: at minimum an assign/release-to-shift action per row; the
  backend ordering contract (overdue first, due date ascending, priority,
  code) already computes the queue order — the UI just never acts on it.
- **Repro:** Open `/schedule/dispatch` with released orders present. Look
  for any button that changes dispatch state. There is none (PL and EN).
- **Severity:** blocker (the view named "dispatch board" cannot dispatch;
  planners fall back to spreadsheets or shouting across the hall).

### O-3 — Order detail mixes planner and operator actions (major)

- **View / step:** `/production/orders/:id`
  (`ProductionOrderDetailView.vue`).
- **Attempted task:** As an operator on a shopfloor tablet, report a
  confirmation without seeing (or misclicking) planner actions.
- **Observed vs expected:** Release / report / complete / close all live in
  one header, gated only by status (`canReport`, `canComplete`, `canClose`).
  There is a density toggle (good for ergonomics), but no role/step
  separation: a confirmation-capable operator also sees complete/close, and
  the planner's release lives next to the operator's report button.
  Expected: progressive disclosure — operator sees report + movements;
  planner sees lifecycle transitions (or a mode switch).
- **Repro:** Open a Released order as a non-admin user. Report, complete and
  release affordances share one toolbar.
- **Severity:** major (misclick risk on the shopfloor; the Operator panel
  exists but does not cover confirmations — see O-9).

### O-4 — Confirmations are order-level, not operation-level (blocker)

- **View / step:** Order detail → "report" modal; confirmations table.
- **Attempted task:** Confirm 50 pcs on operation 20 (welding) while
  operation 10 (cutting) is still running, and track per-operation progress.
- **Observed vs expected:** The confirmation payload is
  `{ productionOrderId, machineId, reportedByOperatorId, reportedAt,
  goodQuantity, scrapQuantity, notes, producedLotId, consumedLots }` — no
  `operationId`. Progress (`producedQuantity`, progress %) aggregates at
  order level. A recipe with a 5-step routing collapses to a single
  quantity counter. Expected: operation-step picker in the modal and
  per-operation progress against the routing.
- **Repro:** Create an order from a 3-operation recipe, report two
  confirmations "for different operations". Both land in one undifferentiated
  list; nothing ties them to routing steps.
- **Severity:** blocker (routing progress — the core promise of a routing —
  is untrackable at execution time).

### O-5 — No RW/PW preview before confirming; IDs may render raw (major)

- **View / step:** Order detail → movements section + per-confirmation
  movements modal.
- **Attempted task:** Before saving a confirmation, see "this will issue
  (RW) 12.5 kg of `MAT-1` from warehouse `W-2` and receive (PW) 50 pcs of
  `P-A`".
- **Observed vs expected:** Movements are shown **after the fact** via `GET
  /api/.../movements` (order-level preview + per-confirmation modal).
  The report modal has no "preview movements" step, so the operator
  confirms blind. Additionally, product/warehouse labels resolve through
  capped lookups (products page 1/100, warehouses 1/100); anything outside
  the cap renders as a raw GUID in the movements table (`productLabel` /
  `warehouseLabel` fall back to the ID). Expected: pre-save preview in the
  modal + server-resolved labels.
- **Repro:** Report a confirmation for an order whose BOM product sorts past
  the first 100 products. Reopen movements — the row shows a GUID instead
  of `code — name`.
- **Severity:** major (blind confirming + unreadable preview rows).

### O-6 — Scrap is captured twice, reason codes live only in one place (major)

- **View / step:** Order detail → report modal (`scrapQuantity` free number)
  vs `/production/scrap` (`ScrapView.vue`, machine + reason code + quantity).
- **Attempted task:** Report 5 scrapped pcs *with reason* `SCRAP-TOLERANCE`
  while confirming work.
- **Observed vs expected:** The confirmation modal takes a bare scrap number
  with no reason-code picker; the scrap registry takes machine + reason +
  quantity but is a separate view with no link to the order/confirmation.
  The two scrap streams are unlinked — Pareto analysis by reason code
  misses in-flow scrap. Expected: reason-code picker (optional) inside the
  confirmation modal, creating a linked scrap event.
- **Repro:** Report a confirmation with scrap = 5. Open `/production/scrap`
  — no corresponding event. Create a scrap event there — it references a
  machine, not the order.
- **Severity:** major (quality loop is broken at the point of capture).

### O-7 — Downtime cannot be recorded in-flow (major)

- **View / step:** Order detail vs `/production/downtime`
  (`DowntimeView.vue`, machine-level start/stop with reason + status).
- **Attempted task:** While confirming order `ZO-100` on machine M-5,
  record a 25-minute breakdown with reason `DT-MAINT-BREAKDOWN`.
- **Observed vs expected:** Downtime is a separate machine-level registry
  (start/stop, reason code, open/closed status) with no order context and no
  entry point from the order detail or the confirmation modal. The operator
  must abandon the order flow, open Downtime, start an event, return, and
  nothing links the two. Expected: "record downtime" action on the order
  (prefilling machine, linking order id) or an order-filter on the downtime
  view.
- **Repro:** Open an InProgress order, look for any downtime affordance.
  There is none; downtime lives only under `/production/downtime`.
- **Severity:** major (downtime attribution to orders — needed for
  availability/OEE honesty — relies on operator memory).

### O-8 — No inline lot registration; trace is opt-in and skippable (major)

- **View / step:** Report modal → produced/consumed lot selects
  (`confirmationLots.ts` rules mirrored from the backend).
- **Attempted task:** Confirm 50 pcs into a *new* lot `L-2026-041` while
  consuming material lot `L-M-009`.
- **Observed vs expected:** Lot dropdowns load `lotService.browse` page
  1/200 — a lot that does not exist yet cannot be created inline; the
  operator must leave to `/production/lots`, create it, and return (losing
  modal state). Worse, lots are optional unless consumed rows exist (backend
  rule: consumed rows require a produced lot; otherwise no lot is needed),
  so the common case — plain good-quantity confirmations — silently skips
  traceability. Expected: "create lot inline" in the modal + a nudge
  (not a block) to attach a produced lot whenever good quantity > 0.
- **Repro:** Open the report modal on a fresh order, enter good quantity
  50, save with no lots. It saves. Genealogy for that output will never
  exist.
- **Severity:** major (traceability gaps are created by default, not by
  exception).

### O-9 — Genealogy is reachable but disconnected from orders (major)

- **View / step:** `/production/lots` → lot detail → genealogy tab
  (`lotGenealogyService.ts` upstream/downstream, depth 1–10, truncation
  badge, `?lotId=&tab=genealogy` deep link — all genuinely good).
- **Attempted task:** From order `ZO-100`, answer "which material lots went
  into output lot `L-2026-041`, and where was `L-2026-041` used downstream?"
- **Observed vs expected:** The genealogy engine works, but order detail has
  no "trace lots" link and the lot view has no "producing order" jump per
  node beyond a text order-code column. The operator must copy a lot code
  from the confirmation flow, navigate to Lots, scan/search it, open the
  genealogy tab. The Operator panel queue (`operatorQueueService.ts`) shows
  orders but no lot/trace affordance either. Expected: bidirectional links —
  order detail → produced/consumed lots → genealogy; genealogy node →
  order detail.
- **Repro:** From an order with lot-linked confirmations, try to reach the
  upstream trace in one click. It takes at least four navigations and a
  copied code.
- **Severity:** major (capability exists but is undiscoverable in-flow;
  recalls/containment actions lose minutes).

### O-10 — Complete/close confirmations lack over/under-production guard (minor)

- **View / step:** Order detail → complete / close confirm dialogs.
- **Attempted task:** Complete order `ZO-100` (planned 1000, produced 120)
  and get warned about the shortfall.
- **Observed vs expected:** The dialogs show generic `confirmComplete` /
  `confirmClose` text with no produced-vs-planned summary and no
  open-confirmation check surfaced. Expected: the dialog states
  produced/planned/remaining and warns on under- or over-production.
- **Repro:** Complete a barely-started order. The dialog asks nothing about
  the 88% shortfall.
- **Severity:** minor (no data loss, but accidental early completion is one
  tap away).

### O-11 — Stale-tab conflicts surface as generic errors (minor)

- **View / step:** Order list + detail (release/complete/close/update all
  send `concurrencyToken`).
- **Attempted task:** Release an order from two tabs; understand the second
  tab's failure.
- **Observed vs expected:** Optimistic concurrency is correctly wired
  (good), but a conflict surfaces as a generic `saveFailed` toast with no
  "data changed — reload and retry" guidance. Expected: a conflict-specific
  message with a reload action.
- **Repro:** Open the same Planned order twice, release in tab A, release in
  tab B. Tab B shows a generic save error.
- **Severity:** minor (correct behaviour, unhelpful message).

### O-12 — Order list lacks progress columns (minor)

- **View / step:** `/production/orders` table.
- **Attempted task:** See which of 30 released orders are behind without
  opening each one.
- **Observed vs expected:** Columns are code / status / planned quantity /
  priority / due date. No produced, remaining, or progress %, although the
  detail endpoint already computes them. Expected: remaining + progress
  columns (and keep the overdue emphasis the dispatch board already has).
- **Repro:** Open `/production/orders` with several InProgress orders. All
  progress insight requires one navigation per order.
- **Severity:** minor (planner overview tax).

### O-13 — Dates/numbers follow the browser, not the app locale (minor)

- **View / step:** Any order/lot/downtime view in PL vs EN.
- **Attempted task:** Get identical date/number rendering for a given app
  locale regardless of OS/browser settings.
- **Observed vs expected:** All user-visible *strings* are properly keyed
  (`$t(...)`, both locales complete for these flows), but dates and
  quantities use bare `toLocaleDateString()` / `toLocaleString()` /
  `Intl.NumberFormat(undefined, …)` — the browser locale, not the app
  locale from the top-bar switch. A browser set to `de-DE` shows German
  dates inside the "PL" app. Expected: pass the active i18n locale to the
  formatters.
- **Repro:** Set browser locale to `de-DE`, app locale to PL, open any
  order. Dates render German-style.
- **Severity:** minor (cosmetic, but it undermines trust in a bilingual
  shopfloor tool).

## Proposed rework plan — (2/2) implementation scope

Principles: recipes define the plan, orders instantiate actual work (per the
business-features map); every increment below is a vertical slice —
backend + frontend + tests — shippable and demoable on the seeded stack in
both locales. Ordered by value/cost. Each increment lists acceptance
sketches for the follow-up analyst spec, not the implementation itself.

### V1 — Order-ready recipes (R-1, R-7, R-8)

- Released-version badge + "no released version" warning in the recipe list
  (data already in the browse payload; display-only).
- Release checklist dialog: operations ≥ 1, outputs defined, BOM products
  active, warehouses set, validity coherent, dependencies acyclic (recheck
  server-side in the release handler; surface as structured warnings).
- Version compare view (operations/BOM/resources/timing deltas between two
  versions).
- Demo: planner sees readiness at a glance, releases v4 with a checklist,
  reviews v2→v3 diff — PL and EN.

### V2 — Guided order creation (O-1, O-12, R-9)

- Product → recipe cascade (recipes filtered/sorted by primary product) +
  mismatch warning when they disagree; version dropdown preselects the
  current released version.
- Order list gains remaining + progress columns (detail endpoint already
  computes them).
- Server-side search for product/recipe/lot autocompletes (replacing the
  100/200-item first-page caps).
- Demo: create `ZO-101` for product A — only compatible released recipes
  offered; list shows live progress — PL and EN.

### V3 — Dispatch that dispatches (O-2)

- Row-level dispatch action on the dispatch board (assign order to
  shift/machine, or sequence within shift) backed by a new order-dispatch
  endpoint; board reflects assignment state.
- Keep the existing backend ordering contract (overdue → due date →
  priority → code); add assignment persistence, not re-sorting.
- Demo: dispatcher assigns three released orders to tonight's shift from
  the board without opening order detail — PL and EN.

### V4 — Operation-level confirmations with RW/PW preview (O-4, O-5)

- `operationId` (routing step) on the confirmation + per-operation progress
  on order detail; confirmation modal gains an operation-step picker
  defaulting from routing order.
- Pre-save RW/PW preview inside the modal (BOM explosion for the entered
  quantity: what will be issued/received, from/into which warehouse),
  reusing the existing movements shape; server-resolved product/warehouse
  labels (no GUID fallback).
- Demo: confirm 50 pcs on operation 20, see the RW/PW lines before saving,
  watch per-operation progress move — PL and EN.

### V5 — In-flow scrap reasons + downtime (O-6, O-7)

- Optional reason-code picker in the confirmation modal that creates a
  linked scrap event (confirmation ↔ scrap event link); scrap registry
  gains an order filter/column.
- "Record downtime" action on order detail (prefills machine, links order);
  downtime view gains an order column/filter.
- Demo: confirm with scrap reason + start a breakdown from the order —
  both visible in their registries with order context — PL and EN.

### V6 — Traceability by default (O-8, O-9)

- Inline lot creation inside the confirmation modal (no navigation loss);
  gentle produced-lot nudge whenever good quantity > 0 (warn, not block).
- Bidirectional links: order detail → produced/consumed lots → genealogy
  tab; genealogy node → order detail; operator queue surfaces lot state.
- Demo: confirm into a brand-new lot without leaving the modal, then walk
  order → lot → upstream trace in two clicks — PL and EN.

### V7 — Planner/operator separation + locale-correct formatting (O-3, O-10, O-11, O-13, R-2, R-5, R-6, R-3-graph)

- Progressive disclosure on order detail (operator mode vs planner mode;
  complete/close guarded with produced-vs-planned summary in the dialog).
- Conflict-specific stale-data message with reload action.
- App-locale-driven date/number formatting (pass the i18n locale to
  `Intl`/`toLocale*` formatters).
- Recipe-creation dialog (source version + change notes + validity),
  skill-ID linkage (or dangling-capability badges), template
  apply-to-existing + drift badges, routing cycle preflight.
- Demo: operator tablet shows report-only UI; planner sees lifecycle with
  shortfall warnings; dates identical for a fixed app locale — PL and EN.

### Explicitly out of the (2/2) scope

Gantt scheduler build, Operator panel rebuild, andon/telemetry/OEE
changes, Playwright smoke-suite changes, and any benchmark against external
MES products — each is a separate tracker gap.

## Verification

- Docs-only slice: no backend, API, database, or frontend production code
  changed; no handler/validator/HTTP contract changed, so no unit tests
  (`tests/AsistOff.MES.Shared.Tests`) and no endpoint integration tests
  (`tests/AsistOff.MES.Integration.Tests`) apply — same test-plan statement
  as the issue.
- `npm run build` (vue-tsc + Vite) must stay green — no source files were
  touched, only `docs/order-flow-review/findings.md` was added.
- Reviewer protocol: open this document, confirm recipe + order flows are
  covered in both locales via the visit log, and spot-check at least three
  friction items by re-walking the stated repro steps on the seeded local
  stack.
