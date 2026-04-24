# Architecture Decision Records

This directory contains Architecture Decision Records (ADRs) for AsistOff MES.

ADRs document **why** a significant architectural or technical decision was made.
They are immutable once accepted — if a decision is superseded, a new ADR is
added that links back to the old one, and the old one is marked as "superseded".

## Format

Each ADR follows this template:

```
# <Number>. <Title>

- Status: Proposed | Accepted | Deprecated | Superseded by ADR‑NNNN
- Date: YYYY‑MM‑DD
- Deciders: <GitHub handles>

## Context

What is the situation? What problem are we solving?

## Decision

The chosen direction.

## Consequences

Positive / negative outcomes, follow‑ups.

## Alternatives considered

What we did *not* pick and why.
```

## Index

| # | Title | Status |
|---|-------|--------|
| [0001](0001-modular-monolith.md) | Modular monolith as the starting architecture | Accepted |
| [0002](0002-multi-tenancy-strategy.md) | Shared database with `TenantId` + EF global query filter | Accepted |
| [0003](0003-auth-jwt-and-rbac.md) | JWT Bearer authentication & interim RBAC via `IsTenantAdmin` | Accepted |
