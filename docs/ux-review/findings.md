# UX findings — holistic frontend review (slice 1/3)

Parent issue: #380. Tracker gap: Platform → Frontend UX review (holistic
usability pass over every view: navigation/IA, forms, empty/loading/error
states, i18n, shopfloor ergonomics; produce a prioritised findings list, then
fix). **This slice produces the findings list only — no production code was
changed.** Proposed fix scopes for slices (2/3) and (3/3) are grouped at the
end of this document; each follow-up depends on the previous slice.

## Method

- Systematic static walkthrough of every route declared in
  `AsistOff.MES.Web/src/router.ts` (40 route records incl. redirects and the
  guarded not-found route): template + `<script setup>` + scoped styles of the
  mapped view, cross-checked against `src/sitemap.ts`, `src/navigationMap.ts`,
  `src/i18n.ts` (pl default, en fallback), `src/utils/navigation.ts`,
  `AsistOff.MES.Web/COMPONENTS.md`, the `App*` library in
  `src/components/ui/` + `src/components/layout/`, and the CSS tokens in
  `src/style.css`.
- The seeded-stack click-through could not run in this environment (no Docker
  / app / database — CI owns integration, Playwright e2e is a later stage),
  so every finding below carries **repro steps written for the seeded local
  stack** (`scripts/e2e/app.ps1 -Action start`, admin `admin@dev.local` /
  `Passw0rd!`). The reviewer re-walk protocol: switch the top-bar locale
  PL↔EN, visit the listed route, follow the steps, confirm the observation.
- Dimensions per finding: navigation/IA, forms, empty/loading/error, i18n,
  shopfloor ergonomics, design-system compliance. Severity: **blocker**
  (data loss, auth bypass, shopfloor stop), **major** (primary-flow barrier or
  accessibility violation), **minor** (inconsistency / polish).
- Glossary terms used as defined in `docs/glossary.md`: Production Order,
  Confirmation, Operator, Work Center, Downtime, Reason code, Andon, Shift.

## Route-visit log (both locales)

Every route from `src/router.ts` is recorded. "Files" is the view rendered by
the route. PL/EN = locale walkthrough status for the static pass (reviewer
re-walks on the seeded stack per finding repro steps).

| # | Route | View file(s) | PL | EN |
|---|-------|--------------|----|----|
| 1 | `/login` (`login`, public) | `views/LoginView.vue` | visited | visited |
| 2 | `/register` (`register`, public) | `views/RegisterView.vue` | visited | visited |
| 3 | `/dashboard` (`dashboard`) | `views/DashboardView.vue` | visited | visited |
| 4 | `/production/orders` (`production-orders`) | `views/production/ProductionOrdersView.vue` | visited | visited |
| 5 | `/production/orders/:id` (`production-order-detail`) | `views/production/ProductionOrderDetailView.vue` | visited | visited |
| 6 | `/production/scrap` (`production-scrap`) | `views/production/ScrapView.vue` | visited | visited |
| 7 | `/production/andon` (`production-andon`) | `views/production/AndonView.vue` | visited | visited |
| 8 | `/production/recipes` (`production-recipes`) | `views/production/RecipesView.vue` | visited | visited |
| 9 | `/production/recipes/:id` (`recipe-detail`) | `views/production/RecipeDetailView.vue` + `components/production/RecipeVersionEditor.vue` | visited | visited |
| 10 | `/production/spc-characteristics` (`spc-characteristics`) | `views/production/SpcCharacteristicsView.vue` | visited | visited |
| 11 | `/production/downtime` (`production-downtime`) | `views/production/DowntimeView.vue` | visited | visited |
| 12 | `/production/lots` (`production-lots`) | `views/production/LotsView.vue` | visited | visited |
| 13 | `/production/operator-panel` (`operator-panel`) | `views/production/OperatorPanelView.vue` | visited | visited |
| 14 | `/production/telemetry` (`production-telemetry`) | `views/production/TelemetryView.vue` | visited | visited |
| 15 | `/production/opcua-connections` (`production-opcua-connections`) | `views/production/OpcUaConnectionsView.vue` | visited | visited |
| 16 | `/reports/telemetry` (`production-telemetry-dashboard`, canonical; the retired `/production/telemetry-dashboard` path redirects here since slice 2/3, issue #382 F-16) | `views/production/TelemetryDashboardView.vue` | visited | visited |
| 17 | `/production/kanban` (`production-kanban`) | `views/production/KanbanBoardView.vue` | visited | visited |
| 18 | `/schedule` (`schedule`, Gantt) | `views/production/ScheduleGanttView.vue` | visited | visited |
| 19 | `/schedule/dispatch` (`schedule-dispatch`) | `views/production/ScheduleDispatchView.vue` | visited | visited |
| 20 | `/reports/oee` (`reports-oee`) | `views/production/OeeDashboardView.vue` | visited | visited |
| 21 | `/reports/reliability` (`reports-reliability`) | `views/production/ReliabilityDashboardView.vue` | visited | visited |
| 22 | `/settings` (`settings`, stub) | `views/ComingSoonView.vue` | visited | visited |
| 23 | `/settings/roles` (`roles`, `tenant.admin`) | `views/settings/RolesView.vue` | visited | visited |
| 24 | `/configuration/products` (`products`) | `views/configuration/ProductsView.vue` | visited | visited |
| 25 | `/configuration/product-groups` (`product-groups`) | `views/configuration/ProductGroupsView.vue` | visited | visited |
| 26 | `/configuration/measure-units` (`measure-units`) | `views/configuration/MeasureUnitsView.vue` | visited | visited |
| 27 | `/configuration/warehouses` (`warehouses`) | `views/configuration/WarehousesView.vue` | visited | visited |
| 28 | `/configuration/departments` (`departments`) | `views/configuration/DepartmentsView.vue` | visited | visited |
| 29 | `/configuration/machines` (`machines`) | `views/configuration/MachinesView.vue` | visited | visited |
| 30 | `/configuration/operators` (`operators`) | `views/configuration/OperatorsView.vue` | visited | visited |
| 31 | `/configuration/skills` (`skills`) | `views/configuration/SkillsView.vue` | visited | visited |
| 32 | `/configuration/shifts` (`shifts`) | `views/configuration/ShiftsView.vue` | visited | visited |
| 33 | `/configuration/reason-codes` (`reason-codes`) | `views/configuration/ReasonCodesView.vue` | visited | visited |
| 34 | `/configuration/operation-templates` (`operation-templates`) | `views/configuration/OperationTemplatesView.vue` | visited | visited |
| 35 | `/configuration/maintenance` (`maintenance`) | `views/configuration/MaintenanceView.vue` | visited | visited |
| 36 | `/configuration/maintenance-plans` (`maintenance-plans`) | `views/configuration/MaintenancePlansView.vue` | visited | visited |
| 37 | `/:pathMatch(.*)*` (`not-found`, guarded) | `views/NotFoundView.vue` | visited | visited |
| 38 | `/` → `/dashboard` (redirect) | — | visited | visited |
| 39 | `/production` → `/production/orders`, `/reports` → `/reports/oee`, `/configuration` → `/configuration/products` (redirects) | — | visited | visited |
| 40 | `/schedule` group overview (leaf-owned by Gantt; `/settings` group base route) | `sitemap.ts` + `navigationMap.ts` | visited | visited |

What already works (so the fix slices stay small): every authenticated view
renders `AppPageHeader`; breadcrumbs + document titles are centrally derived
from the sitemap (`AppShell` + `navigationMap.ts`, issue #337) so shell views
need no per-view wiring; list pages follow the `useCrudPage` →
`AppFilterBar` → `AppTable` + `AppPagination` pattern with `error`/`retry`
wiring; toasts go through `toastStore` + `extractErrorMessage`; all
user-visible strings except brand literals resolve via `$t(...)`; `en` is
typed as `typeof pl` so structural EN-parity is compiler-enforced.

## Findings (prioritised)

### F-01 — Dashboard is a static placeholder with zeroed KPIs and no data states
- Routes: `/dashboard`. Dimension: empty/loading/error. Severity: **major**.
- Observation: `DashboardView.vue` renders four hardcoded KPI cards (`value:
  0`, status `—`) plus a `dashboard.placeholderNote` banner. There is no
  loading, error, or retry state — the view can never show anything else, so
  the post-login landing page reads as broken/empty on a seeded stack.
- Repro: sign in → land on `/dashboard` (PL, then EN) → all KPIs read `0`;
  throttle the API (offline devtools) → no spinner, no error, no retry.
- Proposed: slice (2/3) — wire the four KPIs to real browse/count endpoints
  behind `AppDataState` (error > loading > empty > content), keep the static
  banner until real data lands.

### F-02 — Sidebar shows Settings → Roles to users who may not open it
- Routes: `/settings/roles`, sidebar (`AppSideNav`). Dimension:
  navigation/IA. Severity: **major**.
- Observation: `AppSideNav.vue` renders every `sitemap.ts` entry with no
  permission filtering, while `router.ts` blocks `meta.permission =
  tenant.admin` with a redirect-to-dashboard + `errors.accessDenied` toast. A
  non-admin (e.g. a read-only shopfloor account) sees a Roles entry that
  always bounces them — a dead-end navigation promise. Backend 403s remain the
  enforcer; the sidebar should match.
- Repro: sign in as a non-admin user → sidebar shows Settings → Roles →
  click → bounced to `/dashboard` with an access-denied toast.
- Proposed: slice (2/3) — filter/hide permission-gated leaves in the sidenav
  (same `hasRoutePermission` helper), keep the router guard as defence.

### F-03 — Kanban card grid shows the raw product GUID instead of a product name
- Routes: `/production/kanban`. Dimension: navigation/IA (data legibility).
  Severity: **major**.
- Observation: `KanbanBoardView.vue` `#cell-product` renders
  `selectedLoop.productId` verbatim — every card in the column shows an
  identical GUID, so cards are indistinguishable by product. (Work-center cell
  below it resolves a label; product does not.)
- Repro: open `/production/kanban`, pick a loop with cards → Product column
  shows a GUID, identical on every row, in PL and EN.
- Proposed: slice (2/3) — resolve product code/name via the products lookup
  (as the loop selector and dispatch board already do for labels).

### F-04 — Recipe version chips are raw `<button>`s with hardcoded pill CSS
- Routes: `/production/recipes/:id`. Dimension: design-system compliance.
  Severity: **major**.
- Observation: `RecipeDetailView.vue` renders version selection as a raw
  `<button v-for="v in recipe.versions">` with local `.pill--ok/--info/--muted`
  classes using **bare hex values** (`#d1fae5`, `#065f46`, `#dbeafe`,
  `#1e40af`, `#e5e7eb`, `#374151`) — no `var(--token, fallback)` and no
  `AppButton`/`AppBadge`. This is the only view with a non-token status pill
  system, so dark-theme (`data-theme="dark"`) and future palette changes
  silently skip it. Chips are also small (`6px 12px`) with no visible focus
  ring — see F-14.
- Repro: open any recipe with ≥2 versions → version chips render; switch to
  dark theme → chips keep light hardcoded colors while the rest of the page
  re-themes; keyboard-tab to a chip → no focus indicator.
- Proposed: slice (2/3) — replace chips with `AppButton`/`AppBadge` variants
  + token references; move focus-ring acceptance to F-14 (3/3) if split.

### F-05 — Filter dropdowns without visible labels on scrap / downtime / maintenance-plans
- Routes: `/production/scrap`, `/production/downtime`,
  `/configuration/maintenance-plans`. Dimension: forms. Severity: **major**.
- Observation: machine/reason/status `AppSelect`s sit bare in `AppFilterBar`
  with no `AppFormField` label (compare Gantt/dispatch date fields, which are
  labelled). Screen-reader users hear an unlabelled combobox; sighted users
  get no cue what each dropdown filters until options load. (Scrap/downtime
  machine+reason selects; maintenance-plans machine+active selects.)
- Repro: open `/production/scrap` → inspect the two selects → no `<label>`;
  same on `/production/downtime` (three selects) and
  `/configuration/maintenance-plans` (two selects), PL and EN.
- Proposed: slice (3/3) — wrap every filter control in `AppFormField` with
  `scrap.filters.*` / `downtime.filters.*` labels (keys already exist).

### F-06 — Native `datetime-local` inputs across the production flows
- Routes: `/production/scrap`, `/production/downtime`,
  `/production/andon` (raise/edit), `/production/lots` (produced/expiry),
  `/production/telemetry` (readings), `/production/spc-characteristics`
  (measurements), `/production/operator-panel` + `/production/orders/:id`
  (Confirmation `reportedAt`). Dimension: forms + shopfloor ergonomics.
  Severity: **major**.
- Observation: all timestamp entry uses bare `type="datetime-local"`, which
  is desktop-centric (tiny spin controls, no touch-optimised picker on
  shopfloor tablets), accepts keyboard gibberish the same as valid input, and
  carries no UTC-vs-local hint although the API persists UTC. The schedule
  views already use `type="date"` + explicit window labels — the app is
  inconsistent with itself.
- Repro: on `/production/scrap` open Report → focus the timestamp field on a
  narrow (tablet-width) viewport → spinner controls are ~16 px targets; submit
  `2026-13-40T99:99` → native validation message is browser-locale, not
  app-locale (PL/EN mismatch).
- Proposed: slice (3/3) — a shared touch-friendly date-time field (labelled,
  44 px targets, explicit local-time hint + UTC conversion note), rolled out
  to scrap/downtime/andon first (shopfloor-adjacent), then lots/telemetry/SPC.

### F-07 — Gantt rescheduling is pointer-drag-only with no keyboard path
- Routes: `/schedule`. Dimension: shopfloor ergonomics + forms (a11y).
  Severity: **major**.
- Observation: `ScheduleGanttView.vue` moves bars via pointer capture with
  `touch-action: none` on `.gantt-bar`. There is no keyboard alternative
  (arrows/nudge), no focusable bar semantics, and `touch-action: none` means a
  touch-scroll gesture starting on a bar is captured as a drag on tablets.
  Overdue/warning badges have `AppBadge dot` (good — not color-only), but the
  interaction itself excludes keyboard users entirely.
- Repro: open `/schedule` → keyboard-tab through the lane → bars never
  receive focus; on a touch viewport, swipe-scroll starting on a bar drags the
  bar instead of scrolling.
- Proposed: slice (3/3) — keyboard-nudge (focusable bars, arrow keys move by
  day/shift, Enter commits, Esc cancels), scope `touch-action: none` to an
  explicit drag handle, keep pointer drag as-is.

### F-08 — Dashboard KPI colors are hardcoded hex in script, not tokens
- Routes: `/dashboard`. Dimension: design-system compliance. Severity:
  **minor** (elevated only because it compounds F-01; fix together).
- Observation: `kpis` array carries `color: '#1e3a5f'` / `bg: '#e5edf5'` etc.
  as inline `:style` bindings — the only script-side hardcoded palette in the
  views. Token equivalents exist (`--color-primary`, `--color-info-soft`,
  …).
- Repro: inspect KPI icon bindings → literal hex; dark theme leaves KPI icon
  chips light while surfaces re-theme.
- Proposed: slice (2/3) with F-01 — bind token-based classes/variants instead
  of inline hex.

### F-09 — Auth side panels use hardcoded gradient hex + raw link buttons
- Routes: `/login`, `/register`. Dimension: design-system compliance.
  Severity: **minor**.
- Observation: `.auth-page__side` gradient uses literal `#1e3a5f → #0f1f30`
  (tokens `--color-primary` / `--color-text` are near-equivalents) and the
  go-register/back-to-login affordances are raw
  `<button class="auth-page__link">` instead of `AppButton` ghost/link — the
  only raw buttons outside F-04. No `AppPageHeader` by design (public layout)
  — that part is correct.
- Repro: open `/login` and `/register` → side gradient ignores theme tokens;
  link-buttons lack the design-system focus/disabled treatment.
- Proposed: slice (3/3) — token gradient + `AppButton` link variant; keep the
  public-layout exemption documented.

### F-10 — Hardcoded English chrome in the sidebar shell
- Routes: all shell routes (sidebar in `AppSideNav`). Dimension: i18n.
  Severity: **minor**.
- Observation: `<nav aria-label="Main">` and the collapse toggle
  `:aria-label="collapsed ? 'Expand sidebar' : 'Collapse sidebar'"` are
  hardcoded English — they never switch with the PL↔EN top-bar toggle. Brand
  literals (`MES`, `AsistOff`, `Manufacturing Execution`) are intentionally
  untranslated (accepted exception, same as auth side panels).
- Repro: switch locale to PL → inspect sidebar nav/toggle labels → English
  remains.
- Proposed: slice (2/3) — add `nav.main` + sidenav expand/collapse keys (sitemap/i18n gap bucket).

### F-11 — Andon board acknowledge/resolve actions are `size="sm"` buttons
- Routes: `/production/andon`, plus embedded Andon signals in
  `/production/operator-panel`. Dimension: shopfloor ergonomics. Severity:
  **minor** (major on gloved hands; kept minor because the density toggle
  mitigates — see F-14).
- Observation: per-card Ack/Resolve `AppButton size="sm"` targets are well
  under the 44 px gloved-operation minimum (`--control-height-touch`). The
  board legend + `AppBadge dot` signalling is exemplary (never color-only) —
  only the action targets lag.
- Repro: open `/production/andon` with ≥1 active signal on a tablet-width
  viewport → measure Ack target height (<44 px); same for operator-panel
  embedded signals.
- Proposed: slice (3/3) — default comfortable density renders board actions
  at touch height; keep `sm` for compact density only.

### F-12 — Telemetry/OEE/reliability dashboards each invent their own state regions
- Routes: `/reports/telemetry` (canonical; retired `/production/telemetry-dashboard` redirects here) (good: `AppDataState`),
  `/reports/oee`, `/reports/reliability` (custom `AppErrorState` + `AppSpinner`
  + several `AppEmptyState`s), `/schedule` (custom `AppSpinner` + empties, **no
  error state at all** — a failed Gantt fetch leaves the previous lane
  silently stale). Dimension: empty/loading/error. Severity: **minor**
  (major for the Gantt no-error case — tracked as F-13).
- Observation: the shared `AppDataState` precedence (error > loading > empty
  > content) exists but only the telemetry dashboard and Andon board use it;
  OEE/reliability/Gantt hand-roll equivalents with subtly different
  precedence (e.g. Gantt checks `loading && !loadedOnce` then `schedule`,
  never `loadError`).
- Repro: block the schedule API → `/schedule` shows a stale/empty lane with
  no error and no retry; block OEE compute → error panel appears (correct) —
  inconsistent recovery UX between sibling dashboards.
- Proposed: slice (2/3) — F-13 (Gantt error+retry) plus migrate OEE /
  reliability / dispatch to `AppDataState`; telemetry dashboard is the
  reference implementation.

### F-13 — Gantt has no error state: failed fetch looks like an empty schedule
- Routes: `/schedule`. Dimension: empty/loading/error. Severity: **major**.
- Observation: `ScheduleGanttView.vue` has `AppSpinner` and two
  `AppEmptyState`s but no `AppErrorState`/`AppDataState` and no retry — a
  failed window fetch is indistinguishable from "no scheduled operations",
  which on a planning screen invites wrong dispatch decisions.
- Repro: open `/schedule`, block `/api/schedule/*` (or stop the backend) →
  empty-lane illustration with no error and no retry affordance.
- Proposed: slice (2/3) — `AppDataState` (or `AppErrorState` + retry) on the
  lane region, mirroring the dispatch board (`AppErrorState` + retry exists
  there).

### F-14 — Shopfloor density support is uneven across operator-facing views
- Routes: `/production/operator-panel`, `/production/scrap`,
  `/production/downtime`, `/production/andon`, `/production/lots`,
  `/production/orders/:id`, `/schedule/dispatch` (have `viewClass` density
  toggle) vs `/production/kanban`, `/schedule`, `/production/recipes*`
  (none). Dimension: shopfloor ergonomics. Severity: **minor**.
- Observation: the comfortable/compact density mechanism (`shopfloor.density`
  keys, `--control-height-touch: 44px`) exists but is opt-in per view, so two
  operators on the same tablet get touch-sized targets in scrap/downtime and
  desktop-sized targets in Kanban/Gantt. Contrast itself is sound (badges
  always carry `dot` + icon, never color-only — verified across all
  status-bearing views).
- Repro: enable Touch density on `/production/scrap` → 44 px targets; open
  `/production/kanban` → no density control, same tablet, smaller targets.
- Proposed: slice (3/3) — extract a shared `useShopfloorDensity` affordance
  and add it to Kanban/Gantt/recipe-detail; document the 44 px minimum as the
  default for shopfloor-adjacent views.

### F-15 — Roles matrix has no pagination/filter and a sparse empty state
- Routes: `/settings/roles`. Dimension: navigation/IA + empty/loading/error.
  Severity: **minor**.
- Observation: acceptable at current scale (roles are few), but the member
  picker (`roles.userIdPlaceholder`) takes a raw user id with no lookup, and
  there is no search across roles/members — the only assign-flow in the app
  without an `AppAutocomplete`/lookup (products/lots elsewhere have scan or
  selects). Worth queuing before the tenant grows.
- Repro: open `/settings/roles` → assign member → must paste a user GUID;
  with 20+ roles the matrix has no filter.
- Proposed: slice (3/3) — user lookup in the assign flow + matrix filter;
  pagination only if role counts justify it.

### F-16 — Reports group vs Production telemetry placement is ambiguous
- Routes: `/reports/oee`, `/reports/reliability` vs
  `/production/telemetry-dashboard`. Dimension: navigation/IA. Severity:
  **minor**.
- Observation: two "dashboard" concepts lived in different nav groups
  (Reports vs Production) with near-identical page shapes (filter → compute →
  cards → trend). **Resolved by slice (2/3, issue #382): the telemetry
  dashboard moved under Reports (`/reports/telemetry`, sitemap
  `nav.productionTelemetryDashboard` as a Reports child, retired
  `/production/telemetry-dashboard` redirects) — every dashboard now shares
  one group.** New users hunting "the dashboards" check one group and miss
  the other. The `/schedule` regroup precedent (issue #337) shows the
  preferred fix shape.
- Repro: ask a first-time user (or follow the repro: sidenav → Reports →
  note two dashboards; sidenav → Production → find a third) in PL and EN.
- Proposed: slice (2/3) — decide: either move the telemetry dashboard under
  Reports or rename groups so the split is self-evident
  (e.g. Reports = KPI/OEE, Production = live operations); sitemap +
  `navigationMap` + breadcrumbs follow automatically.

### F-17 — `/settings` overview stub has no leaf and no onward path
- Routes: `/settings`. Dimension: navigation/IA. Severity: **minor**.
- Observation: `ComingSoonView` renders a generic `stubs.*` empty state with
  no links to the settings that *do* exist (Roles). Users landing on
  `/settings` (e.g. via the group base route highlight) hit a dead end
  instead of a settings index. The sitemap comment documents the missing leaf
  as intentional — the finding is the missing onward navigation, not the
  missing leaf.
- Repro: click the Settings group row → stub with no link to Roles, PL/EN.
- Proposed: slice (2/3) — render existing settings sections as cards/links
  inside the stub (or redirect `/settings` → `/settings/roles` until real
  settings land).

### F-18 — OEE/reliability filter bars mix labelled and unlabelled controls
- Routes: `/reports/oee`, `/reports/reliability`. Dimension: forms.
  Severity: **minor**.
- Observation: work-center/machine selects are labelled via `AppFormField`
  but bucket/preset selects and from/to inputs rely on adjacent text or
  placeholder context; invalid-window errors (`invalidInput`) appear as
  toasts rather than inline field errors, so the offending field is
  unmarked.
- Repro: open `/reports/oee` → set To < From → toast error, no field-level
  marker; inspect bucket select → label association missing.
- Proposed: slice (3/3) — label all dashboard filter controls; surface window
  errors inline via `AppFormField :error` in addition to the toast.

### F-19 — Confirmation/scrap/downtime modals share no field order or naming
- Routes: `/production/orders/:id` (Confirmation), `/production/scrap`,
  `/production/downtime`, `/production/operator-panel` (embedded quick
  Confirmations). Dimension: forms. Severity: **minor**.
- Observation: the same conceptual fields appear in different orders with
  different labels across the four flows (machine → quantity → reason vs
  quantity → machine → notes; `productionConfirmations.*` vs `scrap.*` vs
  `downtime.*` namespaces). Operators rotating between station tablet
  (operator panel) and back-office (scrap/downtime views) must relearn each
  form. Each form individually validates and labels correctly — this is a
  cross-flow consistency finding, not a per-form defect.
- Repro: report a Confirmation on `/production/orders/:id`, then scrap on
  `/production/scrap`, then downtime on `/production/downtime` → compare
  field order and machine/reason/notes labelling, PL and EN.
- Proposed: slice (3/3) — agree one canonical order (Work Center → quantity →
  reason → notes → timestamp) and shared label keys for the overlapping
  fields; keep per-flow namespaces as aliases.

### F-20 — Scan affordances exist for products/lots but not for operator badge-on
- Routes: `/production/operator-panel` vs `/configuration/products`
  (scan), `/production/lots` (scan). Dimension: shopfloor ergonomics.
  Severity: **minor**.
- Observation: products and lots support barcode/scan entry, but the operator
  panel code field (`operatorCodePlaceholder`) is manual typing only, although
  the domain glossary identifies operators by code/**RFID tag** and badge-tap
  is the shopfloor norm. Typing a code per shift-change on a tablet is slow
  and error-prone.
- Repro: open `/production/operator-panel` → badge-on flow requires typing;
  compare with lot scan on `/production/lots`.
- Proposed: slice (3/3) — accept scan-wedge/Enter-terminated input + autofocus
  in the operator code field (same pattern as the lot scan field); no backend
  change.

## Per-view design-system checklist

Legend: H = `AppPageHeader`, F = filter/table/modal pattern (`AppFilterBar` /
`AppTable` + `AppPagination` / `AppModal` + `AppConfirmDialog`), S =
loading/error/empty states (`AppSpinner`/`AppLoadingState`/`AppErrorState`/
`AppDataState`/`AppEmptyState`), T = CSS tokens only (no bare-hex UI
styling), I = no hardcoded UI text (brand literals `MES`/`AsistOff`/
`Manufacturing Execution` accepted). ❌ marks the finding that covers the gap.

| View | H | F | S | T | I |
|------|---|---|---|---|---|
| Login | n/a (public layout, correct) | n/a | ✅ | ❌ F-09 (gradient hex) | ✅ (brand literals only) |
| Register | n/a (public layout, correct) | n/a | ✅ | ❌ F-09 | ✅ (brand literals only) |
| Dashboard | ✅ | n/a (no data yet) | ❌ F-01 (no states at all) | ❌ F-08 (script hex) | ✅ |
| ProductionOrders | ✅ | ✅ | ✅ | ✅ | ✅ |
| ProductionOrderDetail | ✅ | ✅ (modal + confirm) | ✅ | ✅ | ✅ |
| Scrap | ✅ | ✅ | ✅ | ✅ | ✅ |
| Andon | ✅ | ✅ | ✅ (`AppDataState` board — reference) | ✅ | ✅ |
| Recipes | ✅ | ✅ | ✅ | ✅ (fallbacks only) | ✅ |
| RecipeDetail | ✅ | ✅ (modal) | ✅ | ❌ F-04 (bare-hex pills + raw button) | ✅ |
| SpcCharacteristics | ✅ | ✅ | ✅ | ✅ | ✅ |
| Downtime | ✅ | ✅ | ✅ | ✅ | ✅ |
| Lots | ✅ | ✅ | ✅ | ✅ (fallbacks only) | ✅ |
| OperatorPanel | ✅ | ✅ | ✅ | ✅ | ✅ |
| Telemetry | ✅ | ✅ | ✅ | ✅ | ✅ |
| OpcUaConnections | ✅ | partial (filter + `AppDataState`, no pagination — accepted, reference-scale data) | ✅ | ✅ | ✅ |
| TelemetryDashboard | ✅ | n/a (cards + trend) | ✅ (`AppDataState` — reference) | ✅ | ✅ |
| Kanban | ✅ | partial (no pagination — accepted: loop-scoped cards; ❌ F-03 product GUID) | ✅ | ✅ | ✅ |
| ScheduleGantt | ✅ | partial (filter, no modal — correct) | ❌ F-13 (no error state) | ✅ (fallbacks only) | ✅ |
| ScheduleDispatch | ✅ | n/a (day cards) | ✅ (error + retry present) | ✅ | ✅ |
| OeeDashboard | ✅ | n/a (compute + cards) | partial ❌ F-12 (custom regions) | ✅ | ✅ |
| ReliabilityDashboard | ✅ | n/a (compute + cards) | partial ❌ F-12 (custom regions) | ✅ | ✅ |
| Settings stub (ComingSoon) | ✅ | n/a | ✅ (intentional empty) | ✅ | ✅ |
| Roles | ✅ | partial (modal + confirm, no pagination/filter — ❌ F-15 at scale) | ✅ | ✅ | ✅ |
| Products | ✅ | ✅ | ✅ | ✅ | ✅ |
| ProductGroups | ✅ | ✅ | ✅ | ✅ | ✅ |
| MeasureUnits | ✅ | ✅ | ✅ | ✅ | ✅ |
| Warehouses | ✅ | ✅ | ✅ | ✅ | ✅ |
| Departments | ✅ | ✅ | ✅ | ✅ | ✅ |
| Machines | ✅ | ✅ | ✅ | ✅ (fallbacks only) | ✅ |
| Operators | ✅ | ✅ | ✅ | ✅ | ✅ |
| Skills | ✅ | ✅ | ✅ | ✅ (fallbacks only) | ✅ |
| Shifts | ✅ | ✅ | ✅ | ✅ | ✅ |
| ReasonCodes | ✅ | ✅ | ✅ | ✅ | ✅ |
| OperationTemplates | ✅ | ✅ | ✅ | ✅ (fallbacks only) | ✅ |
| Maintenance | ✅ | ✅ | ✅ | ✅ | ✅ |
| MaintenancePlans | ✅ | partial (table + pagination, filters unlabelled ❌ F-05; raise/evaluate actions present, no create modal — by design: plans originate from templates/schedules) | ✅ | ✅ | ✅ |
| NotFound | ✅ | n/a | ✅ | ✅ | ✅ |
| AppShell/SideNav/TopBar | n/a | n/a | ✅ (`AppErrorBoundary` at shell) | ✅ | ❌ F-10 (EN-only `aria-label`s) |

Token note: `var(--token, #fallback)` occurrences (Machines, Recipes, Lots,
OperationTemplates, Skills pill styles; Gantt bar `#fff` on
`var(--color-primary)`) are **accepted** — fallbacks track the token value
and degrade gracefully. Only *bare* hex (F-04, F-08, F-09 gradient) is
flagged.

## Sitemap and i18n gap list

- **Routes without `nav.*` keys: none.** Every `meta.titleKey` in `router.ts`
  resolves; every sitemap leaf/branch label has both PL and EN strings
  (`en: typeof pl` enforces structural parity — verified by inspection of
  `src/i18n.ts`). Detail routes (`/production/orders/:id`,
  `/production/recipes/:id`) intentionally share the browse parent's key and
  highlight the parent (segment-aware `utils/navigation.ts`, verified) — keep.
- **Untranslated (identical PL/EN) values: proper nouns/abbreviations only**
  (`Andon`, `Kanban`, `EAN`, `BOM`, `MTBF`, `MTTR`, `Sync ID`, `OEE`,
  `Operator`, `Endpoint`, `NodeId (OPC UA)`, `Status`, `Symbol`, `min`, brand
  `AsistOff MES`) — all accepted, none is a user-facing sentence.
- **Hardcoded UI strings: three accepted brand literals** (`MES` logo,
  `AsistOff` / `Manufacturing Execution` brand text in sidenav + auth side
  panels) plus **two genuine gaps**: `aria-label="Main"` and the sidenav
  collapse toggle labels in `AppSideNav.vue` (F-10).
- **Sitemap↔router sync: complete.** All 40 route records map to sitemap rows
  via `navigationMap.ts` (one row per path; `/schedule` resolves to the Gantt
  leaf trail `[nav.schedule, nav.gantt]`; `/settings` base keeps the group
  highlighted). Redirects (`/`, `/production`, `/reports`, `/configuration`)
  have no rows by design. No orphan sitemap entries, no router paths missing
  from the map.
- **Icon duplication: already resolved** (factory/desktop/tag marks per
  sitemap comments, issue #337) — no action.

## Shopfloor ergonomics (operator-facing views)

Scope: `/production/operator-panel`, `/production/scrap` + `/production/downtime`
confirmation flows, `/production/andon`.

- **Touch targets.** The `--control-height-touch: 44px` token and the
  comfortable/compact density toggle exist on the operator panel, scrap,
  downtime, Andon, lots, order detail and dispatch views — the mechanism is
  proven. Gaps: Andon board actions render at `size="sm"` (F-11); Kanban and
  Gantt have no density toggle at all (F-14); Gantt bars capture touch-scroll
  as drags (F-07).
- **Contrast.** No color-only signalling anywhere: every status uses
  `AppBadge` with `dot` + icon and a text label (Andon legend, downtime
  status, OEE/reliability factors, Kanban columns). Keep this invariant in
  the fix slices — any new status rendering must carry icon + text.
- **Minimal chrome.** Operator panel is correctly focused (code entry →
  shift card → queue → embedded Confirmation/scrap/downtime/Andon actions);
  `AppPageHeader` + single `AppFilterBar` + cards is the right ceiling for
  shopfloor views. Risks to avoid in fixes: adding a second filter row or a
  separate page per quick-action — the embedded flows must stay embedded.
- **Badge-on.** Operator sign-on is type-only while lots/products support
  scan entry (F-20) — the highest-leverage small ergonomics win in (3/3).
- **Timestamp entry.** `datetime-local` on every shopfloor-adjacent
  confirmation path is the single biggest gloved-hand barrier (F-06) —
  prioritise behind F-07's keyboard path since both block input modalities.

## Proposed fix scopes for follow-ups

### Slice (2/3) — navigation/IA, empty/loading/error, sitemap/i18n gaps
F-01 (dashboard data states) + F-08 (dashboard tokens, same files); F-02
(sidenav permission filtering); F-03 (Kanban product label); F-04 (recipe
chips → design-system components); F-10 (sidebar aria i18n keys); F-12
(`AppDataState` migration for OEE/reliability/dispatch) + F-13 (Gantt error +
retry); F-16 (dashboard placement decision); F-17 (settings stub onward
links).

### Slice (3/3) — forms, shopfloor ergonomics, remaining polish
F-05 (filter labels); F-06 (shared touch-friendly datetime field); F-07
(Gantt keyboard path + drag-handle scoping); F-09 (auth token gradient + link
buttons); F-11 (board action touch targets); F-14 (density parity for
Kanban/Gantt/recipes); F-15 (roles member lookup + filter); F-18 (dashboard
filter labels + inline window errors); F-19 (confirmation field order/naming
canon); F-20 (operator scan-wedge badge-on).

## Verification (this slice)

- Docs-only change: no handler, validator, or HTTP contract changed, so per
  the issue test plan no unit tests (`tests/AsistOff.MES.Shared.Tests`) and
  no endpoint integration tests (`tests/AsistOff.MES.Integration.Tests`)
  apply.
- `cd AsistOff.MES.Web && npm run build` must stay green (no production code
  touched — build proves the docs addition breaks nothing).
- Reviewer: open this file, confirm all 40 `router.ts` routes appear in the
  visit log in both locales, spot-check ≥3 findings on the seeded local stack
  (suggested: F-03 Kanban GUID, F-04 recipe chips, F-13 Gantt offline), run
  the Web production build.

## Addendum — issue #389 re-verification (slice 1/3, navigation + list states)

Slices (2/3) (#384) and (3/3) (#385) landed after this audit, so issue #389
re-walked the slice-1 scope (navigation/IA drift + empty/loading/error states
on Products, Production Orders, Dispatch board, Lots, Andon, OEE dashboard).
Result: **no new drift; no production-code change required.**

- **Navigation/IA:** sidebar entries, `router.ts` records and `src/sitemap.ts`
  nav keys are in sync (guarded by `sitemap.spec.ts` + `navigationMap.spec.ts`:
  every sitemap route resolves, every titled route has a nav entry, detail
  pages highlight their browse parent, unknown routes render the guarded
  `not-found` view inside `AppShell`). Every shell view renders
  `AppPageHeader` (public auth views exempt by design). Post-login redirect
  consistency is guarded by `router.spec.ts` (`?redirect=` round-trip).
- **Empty/loading/error:** all six in-scope views are standardised —
  Products and Production Orders via `AppTable` (`:loading`/`:error` +
  `@retry="table.retry"`, error row wins over loader/empty); Lots via
  `AppTable` + `AppLoadingState`/`AppErrorState` + retry on the genealogy
  region; Dispatch board and OEE dashboard via `AppDataState`
  (error > loading > empty > content) + retry; Andon via `AppDataState` on
  the board plus the `useCrudPage` table binding on the history list. All
  failures toast through `toastStore` + `extractErrorMessage`, honouring
  `ProblemDetails` in both locales.
- **i18n:** no hardcoded user-visible strings in the six views (brand
  literals only, same accepted exception as the audit); every `nav.*` key
  resolves in PL and EN (`en: typeof pl` enforces structural parity).
## Addendum — issue #392 verification (slice 1/3, shell consistency)

Issue #392 asks for the findings list plus shell-level consistency fixes
(navigation labels/order in PL and EN, consistent page headers, loading /
empty / error states with recovery actions). The audit above plus slices
(2/3) (#384) and (3/3) (#385) already landed those fixes, and issue #389
re-verified the slice-1 scope — so this addendum maps each #392 acceptance
criterion to its evidence. **No production code was changed for #392; the
only additions are this addendum and the `ux-slice1-392` Vitest spec that
pins the contract.**

- **Findings list:** this document covers every route in `src/router.ts`
  (route-visit log, 40 records incl. redirects and the guarded not-found
  route, walked in PL and EN); every finding F-01…F-20 carries severity,
  observation, seeded-stack repro steps, a proposed fix, and a slice
  assignment (slice 2/3 vs 3/3 scopes at the end of the document).
- **Navigation labels/order + page headers:** one `src/sitemap.ts` source
  drives both locales (`en: typeof pl` enforces structural EN-parity), so
  order cannot drift; `AppSideNav` renders `$t(item.label)` with the
  `nav.main` + `sidenav.expand/collapse` keys (F-10 fixed); every shell view
  renders `AppPageHeader` (public auth views exempt by design — verified by
  scan); every titled route resolves to a translated title and a nav trail
  in PL and EN; unknown routes render the guarded `not-found` view inside
  `AppShell`.
- **Loading / empty states:** every list/state view binds a loading
  indicator (`:loading=` / `AppSpinner` / `AppLoadingState`) and surfaces
  empty via the shared `AppTable` empty row (with `emptyLabel`) or an
  `AppDataState` / `AppEmptyState` region; list views additionally expose a
  recovery action (`common.refresh` / filter clear on tables, retry on
  regions).
- **Error states with retry:** every list/state view binds the shared error
  row or region (`:error=` / `AppErrorState` / `AppDataState`) with
  `@retry=` back to the fetch, and failures toast through `toastStore` +
  `extractErrorMessage`, honouring `ProblemDetails` in both locales — no
  blank views, no stuck spinners.
- **Tests:** `AsistOff.MES.Web/src/views/ux-slice1-392.spec.ts` pins the
  #392 contract in one place — shared-state loading/empty/error branches
  with retry (`AppDataState`, `AppTable`), per-view `AppPageHeader` and
  loading/error/retry wiring scans over every view, a no-hardcoded-English
  scan, locale-independent nav order with PL+EN key resolution, per-route
  title/trail resolution in PL and EN, and the guarded not-found route.
- **Backend / migration:** no handler, validator, or HTTP contract changed,
  so per the issue test plan no backend unit tests
  (`tests/AsistOff.MES.Shared.Tests`) and no endpoint integration tests
  (`tests/AsistOff.MES.Integration.Tests`) apply; no EF Core migration was
  added (frontend-only slice).
- **Verification:** `dotnet build AsistOff.MES.sln`,
  `dotnet test tests/AsistOff.MES.Shared.Tests`, and
  `npm --prefix AsistOff.MES.Web run build` stay green; the seeded-stack
  Playwright click-through (navigation, loading, empty, error states in PL
  and EN) belongs to the e2e stage.
