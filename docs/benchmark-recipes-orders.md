# Benchmark: recipe + Production Order flow vs top-tier MES

Closes #320. Docs-only — no product code, API, or migration changes.

This document compares the AsistOff MES recipe flow (versions/release,
routing/operations, BOM, skills, operation templates) and Production Order
flow (lifecycle, dispatch/scheduling, confirmations, genealogy) against five
leading MES products, using their public documentation and demo references.
Each gap is mapped to our module/view, carries a priority (P0/P1/P2), and
includes a sketch of a follow-up issue slice small enough to become a single
issue.

Related work: issue #319 (usability verification + friction list) covers
*how it feels to click through* our flows; this document covers *what
best-in-class systems do that we don't*. Redesign work should sequence the
#319 friction fixes against the structural gaps below.

Glossary: BOM, Production Order, Operation, Routing, Work Center, Genealogy
(see `docs/glossary.md`).

## 1. Our baseline (what is being compared)

### 1.1 Recipe flow (Production module)

| Aspect | Our implementation | Key files / routes |
|---|---|---|
| Recipe identity | `Recipe` (Code, Name, IsActive, informational `PrimaryProductId`, `CurrentVersionId`) | `AsistOff.MES.Production.Core/Entities/Recipe.cs`, `GET/POST /api/recipes`, `/production/recipes` |
| Versioning + release | `RecipeVersion` Draft → Released → Obsolete; clone-into-draft; release requires ≥1 operation, demotes previous released sibling, sets `Recipe.CurrentVersionId`; non-draft treated as immutable | `RecipeVersion.cs`, `POST /api/recipe-versions`, `/clone`, `/{id}/release`, `RecipeDetailView.vue`, `RecipeVersionEditor.vue` |
| Routing / operations | `OperationNode` with setup/run/teardown/queue timing, `IsOptional`, `AllowParallelExecution` hint, `SortIndex`; dependencies with 4 types (`FinishToStart`, `StartToStart`, `FinishToFinish`, `StartToFinish`) + `LagMinutes` | `OperationsController` (`/api/operations`, `/recipe-versions/{id}/operations/reorder`, `/{id}/dependencies`) |
| BOM | Operation-level `BomItem` (PerUnit/PerBatch/PerOperationRun; AtStart/Continuous/AtEnd; scrap %, optional, preferred-warehouse hint) | `POST /api/operations/{id}/bom-items` |
| Outputs | `OperationOutput` (MainProduct/Coproduct/Byproduct/Scrap/Intermediate) | `POST /api/operations/{id}/outputs` |
| Resources / skills | `ResourceRequirement` (preferred department/machine, `RequiredCapability` as free-text skill display string, `RequiredOperatorCount`); `Skill` dictionary in Configuration; planning hint only, no enforcement | `POST /api/operations/{id}/resources`, `/configuration/skills` |
| Templates | `OperationTemplate` reusable defaults for code/name/type/timing prefill | `GET/POST /api/operation-templates`, `/configuration/operation-templates` |
| Attachments | Polymorphic attachments on recipe objects | `AttachmentsPanel.vue` |

### 1.2 Production Order flow (Production module)

| Aspect | Our implementation | Key files / routes |
|---|---|---|
| Lifecycle | `ProductionOrder` Planned → Released → InProgress → Completed → Closed; totals derived from confirmations; `xmin` optimistic concurrency (409/retry) | `ProductionOrder.cs`, `POST /api/production-orders/{id}/release`, `/complete`, `/close`, `/production/orders`, `ProductionOrderDetailView.vue` |
| Reservations | Soft material reservation on release, relieved on RW confirmation | `StockMovement` / Configuration Stock on hand |
| Dispatch / scheduling | Shift-aware dispatch board (released orders, uncovered-shift flags); Gantt read-model + reschedule + leveling API ("Harmonogram") | `GET /api/schedule/dispatch`, `GET /api/schedule/gantt`, `PUT /api/schedule/gantt/segments/{id}`, `ScheduleDispatchView.vue`, `ScheduleGanttView.vue` |
| Confirmations | Header-level confirmations with RW/PW preview + persist, produced/consumed lots, atomic fan-out (confirmation + movements + genealogy edges + order in one transaction) | `ProductionConfirmationsController`, `ProductionOrderDetailView.vue` |
| Scrap / downtime | Reason-code-linked scrap and downtime capture | `ScrapEventsController`, `DowntimeEventsController`, `ScrapView.vue`, `DowntimeView.vue` |
| Handover | Shift handover logbook (context API + persisted entries) | `ShiftHandoversController` |
| Lots / genealogy | `Lot` registry with status transitions; `LotGenealogyEdge` auto-derived on confirmation; upstream/downstream traceability + lot tree | `LotsController`, `LotGenealogyController`, `LotsView.vue` |
| Operator surface | No dedicated shopfloor panel (tracked as a separate gap row; confirmations happen in the back-office detail view) | — |

## 2. Per-comparator comparison

### 2.1 Siemens Opcenter (Execution Discrete / Process)

Sources: [Opcenter Execution Process](https://www.siemens.com/en-us/products/opcenter/execution/process/)
(product page — materials/formula/recipe/batch management, generic→master
recipe transformation for NPI),
[What's new in Opcenter Execution Process 2507](https://blogs.sw.siemens.com/opcenter/whats-new-in-opcenter-execution-process-2507/)
(process recipes + discrete BOPs in one instance, end-to-end genealogy),
[Opcenter Execution](https://www.siemens.com/en-gb/products/opcenter/execution/)
(sequencing, resource allocation, forward/backward traceability, personnel
certification-aware assignment),
[ERP Research: Siemens Opcenter review](https://www.erpresearch.com/erp-add-ons/manufacturing-mes-plm/siemens-opcenter)
(route enforcement, SAP order/BOM/SOP ↔ confirmation/backflush integration).

- **Recipe flow.** Opcenter manages formulas/recipes/BOMs together with
  material characteristics (acidity, viscosity, shelf life) and transforms
  generic specifications into executable master instructions during NPI.
  Discrete BOPs and process recipes coexist with genealogy from finished good
  to raw material. Route enforcement gates operation start on predecessor
  completion. We match the version/release skeleton and the dependency graph,
  but we have no material-characteristic-aware BOM, no NPI staging
  (generic → master), and our `AllowParallelExecution`/`IsOptional` flags are
  hints nothing enforces.
- **Production Order flow.** Opcenter synchronizes sequencing across the
  supply chain, allocates resources with certification-aware labor
  assignment, and exchanges orders/BOMs/SOPs ↔ confirmations/backflush/goods
  movements bidirectionally with SAP. We match the lifecycle states and the
  dispatch/Gantt scheduling surface, but we release only whole quantities, we
  confirm at header level (no per-operation queue), we don't enforce skill
  certification at confirmation, and we have no ERP order integration
  (`SyncId` is a stub).

### 2.2 SAP Digital Manufacturing Cloud (SAP ME / DMC)

Sources: [Life Cycle of Production Orders](https://learning.sap.com/learning-journeys/executing-processes-for-discrete-industries-in-sap-digital-manufacturing/explaining-the-life-cycle-of-production-orders_cbc18198-d2f0-47ca-8e48-e5d60b20ac61)
(SFC creation at release, partial release, POD execution, non-conformance
routing, inventory put-away),
[Creating and Releasing Orders](https://learning.sap.com/courses/configuring-sap-digital-manufacturing-for-execution-basic-data-and-configuration/creating-and-releasing-orders)
(release statuses: Releasable / Released / Partially Released / Release On
Hold; execution statuses incl. Hold; routing merge scenarios),
[SAP DMC feature description (PDF)](https://help.sap.com/doc/13c9f83611f94a5ab2c94f23cacfc217/latest/en-US/SAP_DMC_FSD_enUS.pdf)
(scheduling/dispatching Gantt with overload display, release from Gantt,
delta rescheduling, order/process execution with backflush of co/by-products,
work instructions + data collection on routings, goods receipts for
finished/co/by-products).

- **Recipe flow.** DMC references BOM + routing per order (order-specific or
  master-data merged, with override scenarios) and enriches routings with
  work instructions and data-collection points executed in the POD. Our BOM
  lives on operations and our routing is richer in dependency types, but we
  have no work-instruction content model on operations (only free-text
  Description), no data-collection points, and no order-vs-master routing
  merge (an order pins one `RecipeVersionId` immutably).
- **Production Order flow.** DMC's unit of execution is the SFC (shop floor
  control) number: release creates trackable SFCs, supports **partial**
  release, hold/unhold with automatic status revert, per-operation start /
  complete in a POD queue, in-process quality inspection, non-conformance
  records that reroute the SFC, and automatic backflush including co- and
  by-products. Our order is the smallest trackable unit — no SFC split, no
  partial release, no hold, no per-operation queue, no non-conformance
  rerouting. Our Gantt matches DMC's planning-board surface (release from
  chart, overload display, reschedule) but DMC additionally auto-adjusts
  delta operations around completed work on ERP up-version changes.

### 2.3 Rockwell FactoryTalk (ProductionCentre / Production)

Sources: [FactoryTalk ProductionCentre](https://www.rockwellautomation.com/en-us/products/software/factorytalk/operationsuite/mes/productioncentre.html)
(master recipes + workflow, work-order management + ERP integration, EBR,
review by exception, warehouse/inventory),
[FactoryTalk Production eBook (PDF)](https://literature.rockwellautomation.com/idc/groups/literature/documents/br/info-br005_-en-p.pdf)
(order planning with work instructions + operator verification, electronic
production record, widget-based operator workflows, automatic genealogy).

- **Recipe flow.** FactoryTalk pairs master recipes with drag-and-drop
  operator workflows (instruction widgets associated to stations/processes/
  operators) so the recipe *is* the operator guidance. Our recipe editor is
  back-office-only: operations carry no executable instruction steps and
  nothing guides an operator through them.
- **Production Order flow.** FactoryTalk verifies operator activity at each
  step (a step "can only begin if the system confirms the proper preceding
  steps are complete"), logs every action into an automatic genealogy +
  electronic production/batch record, and supports **review by exception**
  (approve the record, not every line). Our confirmations are unverified
  header-level quantity reports; we auto-derive genealogy edges (comparable
  strength) but produce no per-order electronic production record and have no
  approval/review gate on release or completion.

### 2.4 Critical Manufacturing MES

Sources: [Complete Modular Solution](https://www.criticalmanufacturing.com/mes-for-industry-4-0/complete-modular-solution/)
(routing/dispatching with service-based dispatch, recipe management,
traceability & genealogy),
[Recipe Management datasheet (PDF)](https://www.criticalmanufacturing.com/wp-content/uploads/2026/04/Recipe-Management_v11.3.pdf)
(central catalog, version compare, shared components, context resolution,
dynamic parameters, equipment download/verify, per-job traceability),
[Genealogic app](https://www.criticalmanufacturing.com/mes-for-industry-4-0/apps/genealogic/)
(interactive graphical genealogy explorer, multi-lot history, recall
exposure analysis),
[semiconductor material model](https://www.criticalmanufacturing.com/industries/semiconductor-manufacturing/)
(hierarchical material transactions: dispatch, track-in/out, split, merge,
rework, hold/release; dual UoM with auto-conversion).

- **Recipe flow.** Critical Manufacturing's recipe system offers a central
  catalog with **version comparison**, shared/reusable recipe components,
  context-based resolution (product → product-group fallback), dynamic
  parameter resolution (business rules / run-to-run), and equipment
  download + integrity verification per SEMI E139. We have versioning and
  templates, but no version diff, no shared operation sub-trees across
  recipes, no resolution context (an order pins an explicit version), and no
  equipment integration.
- **Production Order flow.** Dispatching is service-based: resources provide
  services, materials require them, and the match decides what runs where —
  a superset of our preferred-machine hint. The material model supports
  split/merge/rework/off-flow/hold as first-class transactions; genealogy is
  an interactive explorer with recall exposure analysis ("which products
  contain this bad lot, across lines/customers/batches"). We have
  upstream/downstream trace + lot tree (good foundation) but no split/merge/
  rework transactions and no forward recall-impact query.

### 2.5 AVEVA MES (incl. Recipe Management, Work Tasks, Batch)

Sources: [AVEVA MES](https://www.aveva.com/en/products/manufacturing-execution-system/)
(real-time schedule + job execution, BOM/pre-weight enforcement, work-order
execution with Work Tasks, finished→raw traceability in minutes),
[AVEVA Recipe Management datasheet (PDF)](https://www.aveva.com/content/dam/aveva/documents/perspectives/datasheets/Datasheet_RecipeManagement.pdf)
(ISA-88 state-aligned recipe procedures, versioned templates with approval
gates, done-by/check-by e-signatures, version-difference views, golden-batch
reports),
[AVEVA MES 2023 R2 docs](https://docs.aveva.com/bundle/manufacturing-execution-system/page/1237497.html)
(work orders + jobs model, status-change effects on job states, line
reassignment),
[MES for Food Producers](https://www.aveva.com/en/products/mes-for-food-producers-solution-practice/)
(prebuilt Order/Recipe/Batch practice with scanner/weighing integration).

- **Recipe flow.** AVEVA versions *everything* (equipment, capability,
  formula, recipe templates) with approval-before-production-use and a full
  audit trail, aligns execution with ISA-88 states, and captures golden-batch
  setups for replication. Our release flow has no approval step (single user
  releases), no template versioning cascade, and no execution-history report
  per recipe version.
- **Production Order flow.** AVEVA separates **work orders from jobs**
  (an order decomposes into executable jobs with their own states, and
  status changes propagate), reassigns orders across lines, enforces BOM /
  pre-weight recipes at execution, and digitizes SOP execution via Work
  Tasks with e-signatures. Our order has no job decomposition, no line
  reassignment distinct from rescheduling, no BOM enforcement beyond the
  preferred-warehouse hint, and no signed SOP steps.

## 3. Comparison matrix

"Us" = AsistOff MES at `master` (September 2026). ● = has, ◐ = partial, ○ = missing.

| # | Capability | Us | Siemens Opcenter | SAP DMC | Rockwell FT | Critical Mfg | AVEVA MES | Sources |
|---|---|---|---|---|---|---|---|---|
| R1 | Recipe versioning + release | ● | ● | ● (BOM/routing versions) | ● (master recipes) | ● (catalog + access control) | ● (versioned templates + approval) | §2.1–§2.5 |
| R2 | Recipe version comparison (diff) + change history | ○ (`ChangeNotes` only) | ● | ● | ● | ● (any-two-versions compare) | ● (difference views) | [Critical RM PDF](https://www.criticalmanufacturing.com/wp-content/uploads/2026/04/Recipe-Management_v11.3.pdf), [AVEVA RM PDF](https://www.aveva.com/content/dam/aveva/documents/perspectives/datasheets/Datasheet_RecipeManagement.pdf) |
| R3 | Routing with enforcement (gate step start) | ◐ (graph modeled, nothing enforces) | ● (route enforcement) | ● (SFC routed op→op) | ● (preceding-step verification) | ● (flow enforcement) | ● (ISA-88 state interface) | [Opcenter review](https://www.erpresearch.com/erp-add-ons/manufacturing-mes-plm/siemens-opcenter), [SAP lifecycle](https://learning.sap.com/learning-journeys/executing-processes-for-discrete-industries-in-sap-digital-manufacturing/explaining-the-life-cycle-of-production-orders_cbc18198-d2f0-47ca-8e48-e5d60b20ac61), [FT eBook](https://literature.rockwellautomation.com/idc/groups/literature/documents/br/info-br005_-en-p.pdf) |
| R4 | Structured work instructions on operations | ○ (Description only) | ● (SOPs) | ● (routing enrichment + POD display) | ● (instruction widgets) | ● | ● (Work Tasks SOPs) | [SAP DMC PDF](https://help.sap.com/doc/13c9f83611f94a5ab2c94f23cacfc217/latest/en-US/SAP_DMC_FSD_enUS.pdf), [FT eBook](https://literature.rockwellautomation.com/idc/groups/literature/documents/br/info-br005_-en-p.pdf), [AVEVA MES](https://www.aveva.com/en/products/manufacturing-execution-system/) |
| R5 | Reusable routing/recipe components | ◐ (operation templates = prefill defaults) | ● | ● | ● | ● (shared components, hierarchical flows) | ● (capability library) | [Critical solution](https://www.criticalmanufacturing.com/mes-for-industry-4-0/complete-modular-solution/), [AVEVA RM PDF](https://www.aveva.com/content/dam/aveva/documents/perspectives/datasheets/Datasheet_RecipeManagement.pdf) |
| R6 | Recipe → equipment download / verify | ○ | ● (automation integration) | ● | ● (machine integration) | ● (SEMI E139) | ● (formula download, OPC UA) | [Critical RM PDF](https://www.criticalmanufacturing.com/wp-content/uploads/2026/04/Recipe-Management_v11.3.pdf), [AVEVA RM PDF](https://www.aveva.com/content/dam/aveva/documents/perspectives/datasheets/Datasheet_RecipeManagement.pdf) |
| R7 | Approval / e-signature on release | ○ (single-click release) | ● (roles + rights) | ● | ● (review by exception) | ● (access + change control) | ● (done-by/check-by, 21 CFR Part 11) | [FT ProductionCentre](https://www.rockwellautomation.com/en-us/products/software/factorytalk/operationsuite/mes/productioncentre.html), [AVEVA RM PDF](https://www.aveva.com/content/dam/aveva/documents/perspectives/datasheets/Datasheet_RecipeManagement.pdf) |
| R8 | Skill/certification enforcement at execution | ○ (free-text hint) | ● (certification-aware assignment) | ● (labor qualification alerts) | ● | ● | ● | [Opcenter Execution](https://www.siemens.com/en-gb/products/opcenter/execution/), [SAP DMC PDF](https://help.sap.com/doc/13c9f83611f94a5ab2c94f23cacfc217/latest/en-US/SAP_DMC_FSD_enUS.pdf) |
| O1 | Order lifecycle states | ● (5 states) | ● | ● (release + execution statuses) | ● | ● | ● (work order + job states) | §2.1–§2.5 |
| O2 | Partial-quantity release | ○ (whole order only) | ● | ● (release in increments) | ● | ● | ● | [SAP lifecycle](https://learning.sap.com/learning-journeys/executing-processes-for-discrete-industries-in-sap-digital-manufacturing/explaining-the-life-cycle-of-production-orders_cbc18198-d2f0-47ca-8e48-e5d60b20ac61), [SAP orders](https://learning.sap.com/courses/configuring-sap-digital-manufacturing-for-execution-basic-data-and-configuration/creating-and-releasing-orders) |
| O3 | Per-operation execution queue (SFC/jobs) | ○ (header-level confirmations) | ● | ● (SFC per operation, POD queue) | ● (operator workflows) | ● (track-in/out per step) | ● (work orders → jobs) | [SAP lifecycle](https://learning.sap.com/learning-journeys/executing-processes-for-discrete-industries-in-sap-digital-manufacturing/explaining-the-life-cycle-of-production-orders_cbc18198-d2f0-47ca-8e48-e5d60b20ac61), [AVEVA docs](https://docs.aveva.com/bundle/manufacturing-execution-system/page/1237497.html) |
| O4 | Hold / suspend + resume | ○ | ● | ● (hold/unhold, execution status) | ● | ● (hold/release transaction) | ● | [SAP orders](https://learning.sap.com/courses/configuring-sap-digital-manufacturing-for-execution-basic-data-and-configuration/creating-and-releasing-orders), [Critical semi](https://www.criticalmanufacturing.com/industries/semiconductor-manufacturing/) |
| O5 | Rework / alternate flows | ○ | ● | ● (NC-driven rerouting) | ● | ● (rework + alternate flows) | ● | [SAP lifecycle](https://learning.sap.com/learning-journeys/executing-processes-for-discrete-industries-in-sap-digital-manufacturing/explaining-the-life-cycle-of-production-orders_cbc18198-d2f0-47ca-8e48-e5d60b20ac61), [Critical solution](https://www.criticalmanufacturing.com/mes-for-industry-4-0/complete-modular-solution/) |
| O6 | Split / merge lots | ○ (lots created on confirmation) | ● | ● (SFC split) | ● | ● (split/merge/store/retrieve) | ● | [Critical semi](https://www.criticalmanufacturing.com/industries/semiconductor-manufacturing/) |
| O7 | Co/by-product goods receipts | ◐ (output types modeled, header confirmation) | ● | ● (auto backflush incl. co/by-products) | ● | ● | ● | [SAP DMC PDF](https://help.sap.com/doc/13c9f83611f94a5ab2c94f23cacfc217/latest/en-US/SAP_DMC_FSD_enUS.pdf) |
| O8 | Electronic production/batch record + review | ○ | ● (EBR in Process) | ● | ● (EBR + review by exception) | ● (material history) | ● (execution history + e-signatures) | [FT ProductionCentre](https://www.rockwellautomation.com/en-us/products/software/factorytalk/operationsuite/mes/productioncentre.html), [AVEVA RM PDF](https://www.aveva.com/content/dam/aveva/documents/perspectives/datasheets/Datasheet_RecipeManagement.pdf) |
| O9 | Recall-impact (forward) analysis | ○ (upstream/downstream trace only) | ● (forward/backward trace) | ● | ● (genealogy reporting) | ● (Genealogic exposure analysis) | ● (trace in minutes) | [Opcenter Execution](https://www.siemens.com/en-gb/products/opcenter/execution/), [Genealogic](https://www.criticalmanufacturing.com/mes-for-industry-4-0/apps/genealogic/), [AVEVA MES](https://www.aveva.com/en/products/manufacturing-execution-system/) |
| O10 | Where-used (recipe version → orders) | ○ | ● | ● | ● | ● | ● | §2.1–§2.5 (standard PLM/MES linkage) |
| O11 | ERP order/BOM/routing integration | ○ (`SyncId` stub) | ● (SAP bidirectional) | ● (native S/4HANA replication) | ● (ERP integration) | ● | ● | [Opcenter review](https://www.erpresearch.com/erp-add-ons/manufacturing-mes-plm/siemens-opcenter), [SAP DMC PDF](https://help.sap.com/doc/13c9f83611f94a5ab2c94f23cacfc217/latest/en-US/SAP_DMC_FSD_enUS.pdf), [FT ProductionCentre](https://www.rockwellautomation.com/en-us/products/software/factorytalk/operationsuite/mes/productioncentre.html) |
| O12 | Dispatch/scheduling Gantt | ● (dispatch board + Gantt + leveling) | ● | ● (scheduling + dispatch Gantt) | ● | ● | ● | `GET /api/schedule/dispatch`, `GET /api/schedule/gantt`; [SAP DMC PDF](https://help.sap.com/doc/13c9f83611f94a5ab2c94f23cacfc217/latest/en-US/SAP_DMC_FSD_enUS.pdf) |
| O13 | Golden-batch / per-job recipe trace report | ○ | ● | ● | ● | ● (per-job traceability) | ● (golden batch) | [Critical RM PDF](https://www.criticalmanufacturing.com/wp-content/uploads/2026/04/Recipe-Management_v11.3.pdf), [AVEVA RM PDF](https://www.aveva.com/content/dam/aveva/documents/perspectives/datasheets/Datasheet_RecipeManagement.pdf) |

## 4. Gap list (mapped, prioritised, sliced)

Priority rubric: **P0** — blocks best-in-class order execution or
compliance; highest redesign-sequence weight. **P1** — expected by
evaluators, unlocks measurable efficiency/quality. **P2** — differentiator
or niche; schedule after P0/P1.

### P0

- **G-01 · Partial-quantity release (matrix O2).**
  Our module/view: Production → `Release` handler
  (`POST /api/production-orders/{id}/release`), `ProductionOrdersView.vue`.
  All five comparators release in increments to balance capacity/materials;
  we release the whole order or nothing, which forces planners to split
  orders manually. Follow-up slice: add `quantity` to the release command
  (default = remaining), track `ReleasedQuantity`, derive order status from
  released vs confirmed quantities; UI stepper on the release button.
- **G-02 · Per-operation execution queue (matrix O3).**
  Our module/view: Production → confirmations (`ProductionOrderDetailView.vue`).
  SAP SFCs, AVEVA jobs, FactoryTalk workflows and Critical track-in/out all
  execute *per operation* with a queue; our confirmations are header-level,
  so nobody can see which operation a quantity came from or what is queued
  where. Follow-up slice: derive executable operation instances from the
  pinned recipe version at release; add start/complete per instance with a
  per-Work Center queue read-model (reuse the dispatch-board pattern); keep
  header confirmations working during migration.
- **G-03 · Hold / suspend + resume (matrix O4).**
  Our module/view: Production → order lifecycle + lot status.
  SAP (hold/unhold with status revert) and Critical (hold/release
  transaction) suspend execution without losing state; our only options are
  to leave an order InProgress or close it. Follow-up slice: `OnHold`
  sub-state (or lot-level hold) with hold reason code, blocking
  confirmations/scheduling while held; resume restores prior status.
- **G-04 · Approval gate on recipe release and order completion (matrix R7/O8).**
  Our module/view: Production → `RecipeVersion release`
  (`RecipeDetailView.vue`), order `complete`/`close`.
  Single-click release/completion vs 4-eyes approval (AVEVA done-by/check-by,
  Rockwell review by exception) is the largest compliance gap for regulated
  industries. Follow-up slice: optional per-tenant approval requirement —
  release/completion creates a pending approval, a second user approves;
  record both actors in audit events. Keep single-click the default.

### P1

- **G-05 · Recipe version comparison / diff (matrix R2).**
  Our module/view: Production → `RecipeDetailView.vue` / `RecipeVersionEditor.vue`.
  Critical compares any two versions, AVEVA ships difference views; we show
  `ChangeNotes` prose. Follow-up slice: read-only side-by-side diff of two
  versions (operations, dependencies, BOM, outputs, resources) via a
  `GET /api/recipe-versions/{id}/diff?against={otherId}` read-model; no
  merge semantics.
- **G-05b · Where-used: recipe version → orders (matrix O10).**
  Our module/view: Production → recipe detail + order browse.
  Standard PLM/MES linkage: before obsoleting a version you must see which
  Planned/Released orders pin it. Follow-up slice: `GET /api/recipes/{id}/where-used`
  (or on the version) listing pinning orders with status; link from the
  recipe detail view. May fold into the G-05 issue if small.
- **G-06 · Structured work instructions on operations (matrix R4).**
  Our module/view: Production → operation editor (`RecipeVersionEditor.vue`).
  SAP enriches routings with instructions shown in the POD, FactoryTalk uses
  instruction widgets, AVEVA digitizes SOPs via Work Tasks; our operations
  have free-text Description only. Follow-up slice: ordered instruction
  steps per operation (text + optional attachment ref + acknowledgement
  flag); surface read-only in the order detail/confirmation view.
- **G-07 · Skill enforcement at confirmation/dispatch (matrix R8).**
  Our module/view: Production + Configuration → `ResourceRequirement`,
  `/configuration/skills`, confirmations.
  Opcenter assigns labor certification-aware; SAP raises missing-labor
  alerts. Our `RequiredCapability` is a free-text hint nothing checks.
  Follow-up slice: link `ResourceRequirement` to the `Skill` dictionary by
  id (not display string) and warn/block confirmation when the operator
  lacks the skill (configurable warn vs block per tenant). Needs the
  operator panel (#319-adjacent) for full value — start with the data-model
  link + warning.
- **G-08 · Non-conformance → rework path (matrix O5).**
  Our module/view: Production → scrap capture + order lifecycle.
  SAP reroutes SFCs to special operations on defect data; Critical has
  rework/alternate flows. Our scrap is a dead-end record. Follow-up slice:
  allow a scrapped quantity to spawn a rework operation instance (or
  rework order linked to the parent) carrying the defect/reason code;
  depend on G-02's operation instances.
- **G-09 · Lot split / merge (matrix O6).**
  Our module/view: Production → `LotsView.vue`, `LotsController`.
  Critical's split/merge/store/retrieve and SAP SFC splits handle partial
  processing and batch combination; our lots are only born whole on
  confirmation. Follow-up slice: `POST /api/lots/{id}/split` (quantities)
  and `POST /api/lots/merge` preserving genealogy edges to parents.
- **G-10 · Co/by-product goods receipts on confirmation (matrix O7).**
  Our module/view: Production → confirmations + stock movements.
  SAP backflushes co/by-products automatically; our output types are modeled
  but the confirmation path doesn't post per-output receipts. Follow-up
  slice: on confirmation, post a stock receipt per `MainProduct`/`Coproduct`/
  `Byproduct` output line (PW movement each); keep single-receipt behavior
  behind a flag during migration.
- **G-11 · Forward recall-impact query (matrix O9).**
  Our module/view: Production → genealogy (`LotsView.vue` lot tree).
  Our upstream/downstream trace answers "what went into this lot"; all five
  comparators additionally answer "which finished lots contain this bad
  input" (Critical Genealogic exposure analysis). Follow-up slice:
  `GET /api/lots/{id}/impact` returning downstream finished-good lots with
  quantities/customers, reusing `LotGenealogyEdge` traversal in reverse.

### P2

- **G-12 · ERP order/BOM/routing inbound integration (matrix O11).**
  Our module/view: Production + Gateway → `SyncId` fields (stub today).
  Every comparator replicates orders/BOMs/routings from ERP (SAP-native in
  DMC, gateway-based elsewhere). Follow-up slice (first): define the
  inbound order DTO + idempotent `POST /api/integration/orders` (keyed on
  `SyncId`) creating Planned orders; full bidirectional sync is a program,
  not an issue.
- **G-13 · Golden-batch / per-job recipe trace report (matrix O13/R6).**
  Our module/view: Production → audit events + confirmations.
  AVEVA replicates golden-batch setups; Critical traces recipe parameters
  per job. Follow-up slice: per-order report joining pinned recipe version,
  confirmations, and audit events ("what definition + what actually
  happened"); exportable for audits.
- **G-14 · Standing / repeating orders.**
  Our module/view: Production → order create (`ProductionOrdersView.vue`).
  Repeat-manufacturing shops recreate the same order manually. Follow-up
  slice: "duplicate order" action (new code, same product/recipe/quantity)
  as a stepping stone to full recurrence templates.

### What we deliberately do NOT propose

- Equipment recipe download/verify (SEMI E139 / formula download): our
  telemetry is read-only and there is no equipment-write path; premature
  before any control integration exists. Revisit if an equipment-write
  capability lands.
- Full APS/optimizer scheduling: our Gantt + leveling matches the
  comparators' planning-board surface; true optimization is a separate
  program, not a recipe/order gap.
- Multi-site / enterprise rollouts (model-driven multi-plant MES in AVEVA):
  out of scope for a single-tenant SaaS modular monolith at this stage.

## 5. Prioritised improvement list (for the PR body)

P0: (1) partial-quantity release; (2) per-operation execution queue
(SFC/job instances with per-Work Center queue); (3) hold/suspend + resume;
(4) approval gate on recipe release and order completion.
P1: (5) recipe version diff + where-used; (6) structured work instructions
on operations; (7) skill-id link + enforcement warning at confirmation;
(8) non-conformance → rework path; (9) lot split/merge; (10) co/by-product
goods receipts; (11) forward recall-impact query.
P2: (12) inbound ERP order integration (idempotent, `SyncId`-keyed);
(13) golden-batch / per-job trace report; (14) standing/repeating orders.
Sequencing note: G-02 (operation instances) is the platform for G-08
(rework) and deepens G-10; G-05/G-05b pair naturally; the #319 friction
fixes should land first in the views these gaps will extend
(`ProductionOrderDetailView.vue`, `RecipeVersionEditor.vue`).

## 6. Method + evidence

- Compared our `master` implementation (controllers under
  `AsistOff.MES.Production.Api/Controllers/`, entities under
  `AsistOff.MES.Production.Core/Entities/`, views under
  `AsistOff.MES.Web/src/views/production/`) against the public sources
  linked in §2 and per row in §3. No vendor trial or demo instance was
  used; matrix claims cite the linked pages/PDFs.
- Scope respected: recipes + Production Order flow only; OEE, SPC, CMMS,
  telemetry explicitly excluded per the issue.
- Multi-tenancy impact: none — no entities, no MediatR requests, no
  anonymous-access changes (docs-only).
- Test plan: none (docs-only). Existing backend, frontend, and smoke
  suites stay green via CI; verification below reruns the fast local
  commands only.
