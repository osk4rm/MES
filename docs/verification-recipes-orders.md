# Verification: recipes + Production Order flow usability (issue #319)

Hands-on usability verification of the recipe authoring flow (recipes, versions,
routing, BOM, skills) and the Production Order lifecycle
(create → release → dispatch → confirm → complete → close, plus lot/serial and
genealogy). Follow-up redesign issues should consume the prioritised friction
list at the bottom.

## Method

- **Static walkthrough of the exact code paths** (no live stack): every view,
  component, service call, and seed step below was read at commit `75c848f`
  (`master`). Per the implementing task's constraints (no Docker / app / database;
  CI owns the integration suite, a later stage owns Playwright e2e), no browser
  click-through was run — each friction cites the file and line-range that
  exhibits it, so a reviewer can reproduce it on a seeded stack in minutes.
- **Seed reference:** `scripts/seed/seed-demo-data.ps1` (default `-Orders 24`).
  All seed codes carry a run tag prefix `SD<TAG>` (e.g. `SDAB12-ORD-001` for
  finished-goods orders, `SDAB12-SUP-…` / `SDAB12-INT-…` for supply/intermediate
  orders), finished goods `Widget A/B`, `Gadget X` (`$Prefix-P-WA/WB/GX`),
  machines `SD<TAG>-M-CNC1…`, skills `SD<TAG>-CNC/-WELD/-ASSY/-QC/-PACK`, scrap
  reason codes `SD<TAG>-SC-TOL/-CRK/-SRF`, downtime codes `SD<TAG>-DT-BRK/-ELEC`
  (`seed-demo-data.ps1` lines 226–262, 553–676).
- **Glossary terms used:** BOM, Production Order, Operation, Routing,
  Work Center, Operator, Confirmation.

## Flow results (pass/fail)

| # | Flow (route) | Verdict | Notes |
|---|---|---|---|
| F-A | Recipe CRUD (`/production/recipes`, `RecipesView.vue`) | **Pass with friction** | Create/edit/delete, code+name filters, active pill, pagination all present. Friction: lookup cap F-06, no product/active filters F-14. |
| F-B | Recipe version create/release (`/production/recipes/:id`, `RecipeDetailView.vue` + `RecipeVersionEditor.vue`) | **Pass with friction** | One-click release with inline error, version chips, delete→previous-version navigation and delete/deactivate cleanup prompt all work. Friction: clone source fixed F-11, release of empty/operation-less version not visibly guarded in UI. |
| F-C | Routing edit — operations + dependencies (version editor, dependencies tab) | **Pass with friction** | Add/edit/delete operations, template prefill, 4 dependency types, predecessor selector excluding self/duplicates. Friction: no reorder F-05, advanced op fields not editable in modal (see F-12b), list-style graph F-12. |
| F-D | BOM + outputs edit (version editor, BOM/outputs tabs) | **Pass** | Inline edit, perUnit/perBatch/fixed quantity types, product/byProduct/waste/sample output types. Friction: product label falls back to raw GUID outside first lookup page F-06. |
| F-E | Skill assignment to operations/templates (version editor resources tab; `/configuration/operation-templates`; `/configuration/skills`) | **Fail (data-integrity)** | Assignment stores a free-text snapshot, not a reference — see F-04. Template prefill into operations works; skill dictionary CRUD works. |
| F-F1 | Order create/edit (`/production/orders`, `ProductionOrdersView.vue`) | **Pass with friction** | Released-version-only dropdown (non-released disabled with label), lookups, validation toasts. Friction: product↔recipe mismatch not hinted F-07, lookup cap F-06. |
| F-F2 | Order release (list row action + detail header) | **Pass** | Confirm dialog, released toast, concurrency token sent. Friction: no bulk release, no release from dispatch board F-08. |
| F-F3 | Dispatch board (`/schedule/dispatch`, `ScheduleDispatchView.vue`) | **Pass with friction** | Day/shift cards, uncovered-shift badge, overdue badge, row-click → order detail. Friction: read-only F-08; `/schedule` Gantt vs `/schedule/dispatch` split is now labelled (`nav.schedule` vs `nav.dispatchBoard`) but discoverability untested. |
| F-F4 | Operator confirmations incl. scrap/downtime with reason codes (`/production/orders/:id`, `ProductionOrderDetailView.vue`) | **Fail (flow gap)** | Good/scrap quantities, machine+operator pickers, consumed/produced lots with genealogy hint, RW/PW movement tables per order and per confirmation. Friction: **no reason codes on confirmations** F-02; **downtime not reachable from the order flow** F-03; lot pickers unfiltered F-09. |
| F-F5 | Order complete → close (detail header) | **Pass** | Gated (`canComplete` only InProgress, `canClose` only Completed) with confirm dialogs, progress bar, produced/scrapped/remaining summary. No friction found. |
| F-G | Lot/serial + genealogy (`/production/lots`, `LotsView.vue`) | **Fail (usability)** | Scan-by-code card, status filters, hold/release/scrap transitions, upstream+downstream genealogy with depth + truncation badge. Friction: create/edit form needs raw GUIDs F-01; tables show raw GUIDs. |

## Friction list

### P0 (blocks real use)

**FR-01 — Lot create/edit requires pasting raw GUIDs; tables render raw GUIDs.**
Repro on seed data: seed the stack, open `/production/lots` → **Create**.
Expected: product / measure-unit pickers showing e.g. `SD<TAG>-P-WA — Widget A`.
Actual: `lots.code`/`quantity` are the only human-friendly fields — `productId`
and `measureUnitId` are plain text inputs (`LotsView.vue` lines 88–97,
`AppInput v-model="form.productId"`), so the user must paste entity IDs from
devtools. The lots table likewise has no label template for `productId`
(columns definition lines 327–334) and the detail modal prints raw IDs
(lines 156–163). No role can create a correct lot from the UI alone.
Affected: `/production/lots` (table, create/edit modal, detail modal).

### P1 (serious detour or data-quality risk)

**FR-02 — Order confirmations capture scrap quantity with no reason code.**
Repro: open a released seed order (e.g. `SD<TAG>-ORD-001`) at
`/production/orders/:id` → **Report** → set **Scrap quantity** > 0.
Expected: a scrap reason-code select (seed provides `SD<TAG>-SC-TOL/-CRK/-SRF`,
category Scrap) so per-order scrap keeps its cause.
Actual: the confirmation form (`ProductionOrderDetailView.vue` lines 196–302)
has no `reasonCodeId` field — scrap is a bare number. Reason-coded scrap exists
only as standalone events in `/production/scrap` (`ScrapView.vue`, requires
machine + reason + quantity, order attachable only at create time), i.e. two
parallel scrap paths and order scrap without a cause.
Affected: `/production/orders/:id` (confirmation modal), `/production/scrap`.

**FR-03 — Downtime with reason codes is unreachable from the order flow.**
Repro: from the same order detail, try to record a machine stop with cause
(seed codes `SD<TAG>-DT-BRK/-ELEC`, category Downtime).
Expected: a "report downtime" entry point scoped to this order/machine, or at
least a link.
Actual: downtime lives only in `/production/downtime`, keyed to machine with
start/stop, with no link to or from the order/confirmation flow — the operator
must leave the order, losing context (which order/operation was running).
Affected: `/production/orders/:id`, `/production/downtime`.

**FR-04 — Skill assignment stores a free-text snapshot, not a skill reference.**
Repro: open any recipe version → **Resources** tab → pick a skill (e.g.
`SD<TAG>-WELD — Spawanie`) → **Add**.
Expected: the requirement references the skill (renames/deactivations propagate
or warn; row links to the skill).
Actual: `addResource` (`RecipeVersionEditor.vue` lines 766–780) persists
`requiredCapability: "<code> — <name>"` as text. Renaming the skill in
`/configuration/skills` silently orphans every requirement; there is no
validation that the typed capability exists and no link back to the skill.
Affected: `/production/recipes/:id` (resources tab), `/configuration/skills`.

**FR-05 — Operations cannot be reordered.**
Repro: version editor → operations list shows `sortIndex + 1`
(`RecipeVersionEditor.vue` line 34); add three operations, then try to move the
3rd to 1st place.
Expected: move up/down or drag reorder.
Actual: no reorder control exists (no move/drag code paths; edit preserves
`sortIndex`, create leaves sequencing to the backend). Fixing a sequence means
delete + recreate, which also destroys BOM/outputs/resources attached to those
nodes.
Affected: `/production/recipes/:id` (operations sidebar).

**FR-06 — Lookup dropdowns are capped at the first page with no search.**
Repro: grow the catalog past 100 products (or 100 skills), then create a recipe
(`RecipesView.vue` line 120: `pageSize: 100`), a Production Order
(`ProductionOrdersView.vue` lines 243–247: three `pageSize: 100` browses), or a
BOM line (version editor line 362–364: products/skills 100, templates 200).
Expected: server-side search or paged picker.
Actual: anything beyond the first page is unselectable, and already-saved rows
outside the page render as raw GUIDs (`productLabel` falls back to the id,
`RecipeVersionEditor.vue` lines 354–357; same pattern for predecessors).
Affected: `/production/recipes`, `/production/orders`,
`/production/recipes/:id`.

**FR-07 — Order create allows product/recipe/version mismatches without a hint.**
Repro: `/production/orders` → **Create** → pick product `Widget A` but a recipe
for `Gadget X`; the version dropdown correctly follows the recipe.
Expected: recipes filtered by the chosen product, or a mismatch warning —
only the server rejects it.
Actual: product and recipe pickers are independent (`ProductionOrdersView.vue`
lines 73–87); the user discovers the mismatch via a save error.
Affected: `/production/orders` (create/edit modal).

**FR-08 — Dispatch board is read-only.**
Repro: `/schedule/dispatch` on a seeded week (released `SD<TAG>-ORD-*` rows).
Expected: release an order, flag/assign work, or at least jump to the action.
Actual: the board shows day/shift cards (incl. the uncovered-shift badge) and an
orders table whose only interaction is row-click → order detail
(`ScheduleDispatchView.vue` lines 63–94). No inline release/assign/reorder; the
uncovered badge has no associated action.
Affected: `/schedule/dispatch`.

**FR-09 — Confirmation lot pickers are unfiltered and scan-less.**
Repro: order detail → **Report** → **Produced lot** / **Consumed lots** selects.
Expected: lots filtered to the order's product, preferably with scan input
(the scan card exists — but only in `/production/lots`, `LotsView.vue`
lines 10–27).
Actual: `lotOptions` lists every lot as bare code
(`ProductionOrderDetailView.vue` line 495), so picking a wrong-product lot is
one misclick away and shopfloor scanning can't be used in the flow.
Affected: `/production/orders/:id` (confirmation modal).

**FR-10 — Genealogy is two clicks deep and disconnected from orders.**
Repro: from a confirmation's produced lot, try to see where-used.
Expected: one click from the order/confirmation to the lot's genealogy.
Actual: genealogy requires lot row → **Details** → **Genealogy** tab
(`LotsView.vue` lines 125–146), tables show lot codes without product names,
and there is no link from the order detail or its movement tables to a lot's
genealogy view.
Affected: `/production/lots`, `/production/orders/:id`.

### P2 (polish / discoverability)

**FR-11 — New version always clones the released version or starts blank.**
Repro: `/production/recipes/:id` → **New version** with two drafts and one
released version (`RecipeDetailView.vue` lines 157–171).
Expected: choose the source version (usually the latest draft).
Actual: the released version is cloned when one exists, otherwise a blank
version is created — iterating on an unreleased draft silently discards draft
work from the new version.
Affected: `/production/recipes/:id`.

**FR-12 — Dependency editing is list-based with no visual or cycle feedback.**
Repro: version editor → **Dependencies** tab with 3+ operations.
Expected: a readable sequence/graph and an early warning when a dependency
would create a cycle.
Actual: a flat node list plus per-operation predecessor/type selects with
numeric enums (`RecipeVersionEditor.vue` lines 75–113); only predecessor/
successor counts summarise structure (lines 418–422). Cycle rejection (if any)
surfaces only as a save error. Related: the operation edit modal exposes only
code/name/description/setup/run-per-unit — `operationType`, `runTimeMode`,
teardown/queue times, optional/parallel flags are preserved but not editable
(lines 477–498) — and delete uses a native `confirm()` instead of the app's
`AppConfirmDialog` (lines 522–533).
Affected: `/production/recipes/:id` (dependencies tab, operation modal).

**FR-13 — Recipe ↔ template ↔ skill live in two modules with no cross-links.**
Repro: from a resource row showing `SD<TAG>-WELD — Spawanie`, try to open the
skill; from `/configuration/operation-templates`, try to see which recipes use
a template.
Expected: deep links both ways.
Actual: the template picker is one-way prefill into the operation modal
(lines 451–463); resource rows are dead text (FR-04). Skill/template upkeep
requires manual navigation between Production and Configuration.
Affected: `/production/recipes/:id`, `/configuration/operation-templates`,
`/configuration/skills`.

**FR-14 — List filters miss the fields planners need.**
Repro: `/production/orders` (code + status filters only,
`ProductionOrdersView.vue` lines 10–18), `/production/recipes` (code + name,
`RecipesView.vue` lines 10–13), `/production/lots` (no supplier-lot search).
Expected: orders by product/recipe/due-date, recipes by product/active flag,
lots by supplier lot number.
Actual: plain browsing/paging through seed-scale data to find
e.g. all `Widget A` orders.
Affected: `/production/orders`, `/production/recipes`, `/production/lots`.

## Out of scope (not verified)

Benchmarking against external MES products (separate tracker row / issue #320);
operator-panel ergonomics beyond these two flows; fixes/redesign for the above
(follow-up issues consume this list).

## Multi-tenancy impact

None — docs-only change. No new entities (`ISaasy` unchanged), no new MediatR
requests (no `ITenantRequest` / `IAllowAnonymousRequest` changes), no
anonymous-access changes.

## Test plan

- **Unit tests:** none (docs-only change, per the issue's test plan).
- **Endpoint integration tests:** none new; existing integration suite stays
  green via CI.
- **Manual verification:** the issue IS a verification pass — method, seed
  state, and per-flow verdicts are recorded above. No live click-through was
  run in this environment (no Docker/app/database per task constraints); each
  friction cites exact files/lines for a minutes-long repro on a seeded stack.
  Playwright smoke suite stays green via CI.
