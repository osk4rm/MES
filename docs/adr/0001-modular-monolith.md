# 0001. Modular monolith as the starting architecture

- Status: Accepted
- Date: 2026‑04‑23
- Deciders: core team

## Context

AsistOff MES is a new SaaS platform covering a broad, evolving domain (Manufacturing Execution). Early in the product's life the bounded contexts are not yet stable — BOM, Routing, Production Orders, Shopfloor Execution, Traceability, OEE are all likely to evolve and influence each other. A microservices split at this stage would lock in boundaries that we will inevitably get wrong, paying a high operational cost for speculative flexibility.

At the same time, the architecture must allow decomposition into separately deployable services later, without a full rewrite.

## Decision

We adopt a **modular monolith** structure:

* Single deployable (`AsistOff.MES.Gateway`), single process.
* One PostgreSQL database shared across modules.
* Each business module is a separate .NET assembly (`AsistOff.MES.<Module>.Core/.Application/.Infrastructure/.Api`) and is loaded dynamically by the module loader.
* Modules communicate **only** through:
  * MediatR requests (in‑process commands / queries)
  * Domain events (in‑process dispatcher; outbox pattern is planned for cross‑module async events)
  * Shared contracts (`*.Contracts` projects), **never** by referencing another module's `Core`/`Application`/`Infrastructure`.

## Consequences

**Positive**

- Low operational complexity: one deploy, one database, one log stream.
- Refactoring across modules is fast (in‑process, compile‑time safety).
- Clear physical boundaries (projects) make later extraction straightforward — a module can be pulled out into its own service by replacing its MediatR / event integrations with HTTP / broker calls.

**Negative**

- Discipline required: nothing prevents a developer from adding a cross‑module project reference that breaks the boundary. Addressed by code review and (planned) ArchUnit‑style tests.
- Shared database implies shared schema evolution; this is acceptable at our scale.

## Alternatives considered

- **Microservices from day one** — rejected: premature boundaries, operational overhead unjustified for a small team.
- **"Just a monolith"** (no module boundaries) — rejected: we lose the ability to decompose later without a rewrite; lack of isolation leads to spaghetti over time.

## Follow‑ups

- ADR‑0002: multi‑tenancy strategy (shared DB with tenant filter).
- Future ADR: when to extract a module (heuristics: independent scaling needs, separate team ownership, incompatible tech).
