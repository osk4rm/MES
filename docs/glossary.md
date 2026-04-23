# MES Domain Glossary

A shared vocabulary for the manufacturing domain AsistOff MES operates in.
Entries are sorted alphabetically by the English term; Polish equivalents and
typical abbreviations are provided where relevant.

When working with AI agents, point them at this file so they use the correct
term instead of inventing synonyms.

---

## Andon
A visual signalling system on the shopfloor (lights, dashboards) that alerts
operators, supervisors and maintenance to abnormal conditions (downtime,
quality issue, material shortage).

## Availability
Component of [OEE](#oee). Ratio of actual run time to planned production time.
`Availability = Run Time / Planned Production Time`.

## BOM — Bill of Materials *(PL: Receptura / Struktura wyrobu)*
The hierarchical list of raw materials, sub‑assemblies and quantities required
to produce one unit of a finished good. Versioned and effective‑dated in MES
contexts.

## Confirmation / Zgłoszenie
An operator's report of work performed on a [Production Order](#production-order):
quantity produced, quantity scrapped, time spent, reason codes for scrap /
downtime.

## CMMS — Computerized Maintenance Management System
Maintenance planning and execution system. MES usually integrates with or
subsumes a lightweight CMMS for asset‑related work orders.

## Downtime / Przestój
Any time a [Work Center](#work-center) was scheduled to run but did not.
Classified by cause (setup, maintenance, material, quality, other) via
**reason codes**.

## EAN / GTIN
Global Trade Item Number — a 13‑digit barcode identifying a tradeable product.
Stored on `Product` and used for scanning on the shopfloor.

## Genealogia *(EN: Genealogy / Traceability)*
Full "what went into what, and when" record: which material lots / serials
were consumed by which finished good lot, on which work center, by which
operator, under which production order.

## Kanban
Pull‑based material replenishment signal. Not always present in MES but often
integrated with warehouse movements.

## Lot / Serial *(PL: Partia / Numer seryjny)*
**Lot** = batch of identical units produced together (e.g. 500 units of
product X from run #1234). **Serial** = a unique identifier per single unit.

## MES — Manufacturing Execution System
Software layer between the ERP (planning) and the shopfloor (PLC/SCADA).
Responsibilities: dispatching work, collecting feedback, traceability, OEE,
quality, downtime tracking.

## MTBF — Mean Time Between Failures
Average operating time between failures of a work center. Reliability KPI.

## MTTR — Mean Time To Repair
Average time to restore a work center after a failure. Maintenance KPI.

## OEE — Overall Equipment Effectiveness
`OEE = Availability × Performance × Quality`. The headline manufacturing KPI.
Each factor is a 0–1 ratio.

## OPC UA
Open Platform Communications, Unified Architecture — the standard protocol
for reading tags from PLCs / SCADA. Used by the MES telemetry reader service.

## Operation / Operacja
A single step in a [Routing](#routing) (e.g. "cutting", "welding",
"packaging"). Performed on a [Work Center](#work-center), consumes time and
materials, produces output.

## Operator *(PL: Operator)*
A person executing work on the shopfloor. Identified in AsistOff MES by an
`Operator` entity with a code / RFID tag. **Distinct** from a system `User`
(who logs into the back office UI).

## Performance
Component of [OEE](#oee). Ratio of actual output rate to theoretical maximum.
`Performance = (Total Count × Ideal Cycle Time) / Run Time`.

## Production Order *(PL: Zlecenie produkcyjne)*
An instruction to manufacture a specified quantity of a product by a due date.
Status lifecycle: **Planned → Released → InProgress → Completed → Closed**.
Has planned / produced / scrapped quantities, priority, due date.

## Quality
Component of [OEE](#oee). Ratio of good units to total units produced.
`Quality = Good Count / Total Count`.

## Reason code
A controlled‑vocabulary code attached to a downtime event or scrap
confirmation (e.g. `DT‑MAINT‑BREAKDOWN`, `SCRAP‑TOLERANCE‑OVER`). Enables
Pareto analysis.

## Routing *(PL: Marszruta)*
Ordered sequence of [Operations](#operation) required to produce a product,
including which [Work Center](#work-center) performs each operation, setup
time, run time per unit.

## RW / PW
Polish warehouse movement types, commonly used in AsistOff MES:
* **RW** (*Rozchód Wewnętrzny*) — internal issue: materials consumed by
  production.
* **PW** (*Przyjęcie Wewnętrzne*) — internal receipt: finished goods returned
  to the warehouse from production.

## SCADA
Supervisory Control And Data Acquisition — the control system layer below
MES, collecting signals from PLCs. MES reads from SCADA via [OPC UA](#opc-ua)
or bespoke protocols.

## Shift *(PL: Zmiana)*
A defined working time window (e.g. 06:00–14:00). Operators are scheduled
into shifts; downtime during shift is counted against availability.

## SPC — Statistical Process Control
Quality monitoring using control charts (X‑bar, R, p‑charts, …) to detect
when a process drifts out of statistical control.

## Tenant
A customer organization in the SaaS model. Every tenant‑scoped entity carries
`TenantId`; isolation is enforced by the EF Core global query filter (see
[ADR‑0002](adr/0002-multi-tenancy-strategy.md)).

## Traceability
See [Genealogia](#genealogia-en-genealogy--traceability).

## Unit of Measure *(PL: Jednostka miary, JM)*
How quantity is measured for a product: pieces, kilograms, meters, liters, …
A product may have multiple UoMs with conversion factors (base UoM + alt UoMs).

## Work Center *(PL: Stanowisko / Gniazdo robocze)*
A resource on the shopfloor where [Operations](#operation) happen — a
machine, a workbench, a cell, or a logical grouping. Has a calendar,
capacity, efficiency factor.
