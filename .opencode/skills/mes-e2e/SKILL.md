---
name: mes-e2e
description: Use when the mes-e2e-tester agent runs Playwright smoke tests for an AsistOff MES PR. Provides the scope map, credentials, smoke checklist and verdict format.
---

# MES end-to-end smoke testing

## Prerequisites (single source of truth)

Local DB + stack setup lives in **`docs/e2e-local-setup.md`** — read it first.
One-command DB: `docker compose up -d postgres` (mes / admin / root on
`localhost:5432`). Without it the backend exits at migration time and the
verdict is `E2E_BLOCKED`. `postgres__connectionString` env overrides user
secrets by design.

## Environment

| Thing | Value |
|---|---|
| Frontend | `http://localhost:5173` |
| Backend (http profile) | `http://localhost:5243` |
| Login | `admin@dev.local` / `Passw0rd!` (dev tenant) |
| Start / stop stack | `pwsh -File scripts/e2e/app.ps1 -Action start\|stop\|status` |
| Backend log | `%TEMP%\opencode\e2e-backend.err.log` |
| Frontend log | `%TEMP%\opencode\e2e-frontend.log` |

The stack is driven with the Playwright MCP browser tools. If the backend does
not become healthy (usually PostgreSQL is down), the verdict is `E2E_BLOCKED`.

## Resolving scope

```
gh pr diff <N> --name-only
```

Map the first matching prefix to a smoke area. If several match, test all of
them. If a shared file matches, escalate to **whole system**.

| Changed path prefix | Area | Views to smoke |
|---|---|---|
| `AsistOff.MES.Web/src/views/configuration/Products*` | products | `/configuration/products` |
| `.../ProductGroups*` | product groups | `/configuration/product-groups` |
| `.../MeasureUnits*` | measure units | `/configuration/measure-units` |
| `.../Warehouses*` | warehouses | `/configuration/warehouses` |
| `.../Departments*` | departments | `/configuration/departments` |
| `.../Machines*` | machines | `/configuration/machines` |
| `.../Operators*` | operators | `/configuration/operators` |
| `.../Skills*` | skills | `/configuration/skills` |
| `.../ReasonCodes*` | reason codes | `/configuration/reason-codes` |
| `.../OperationTemplates*` | operation templates | `/configuration/operation-templates` |
| `AsistOff.MES.Web/src/views/production/*` | production / recipes | `/production/recipes` |
| `.../components/layout/*`, `router.ts`, `services/http.ts`, `stores/authStore.ts` | shared | **whole system** |
| anything else | unknown | whole system |

## Smoke checklist

**Whole system (always do the first two):**

1. `/login` renders; log in as the dev tenant; land on `/dashboard`.
2. Side nav renders all groups and navigating does not throw.
3. Browser console has no uncaught errors on the pages visited.

**Per area:** open the view, confirm the list/table renders, and run the happy
path the PR changed (create / edit / delete / open detail). Capture the visible
result.

## Evidence to include in the PR comment

- PR number and resolved scope (which areas, why).
- For each flow: URL, action, observed result, PASS/FAIL.
- Console errors with their text.
- Any flow you could not run and why.
- Final line: `VERDICT: E2E_PASS` | `VERDICT: E2E_FAIL` | `VERDICT: E2E_BLOCKED`.

## Rules

- Only the changed scope plus the whole-system basics. No full regression.
- A flow you could not execute is `E2E_BLOCKED`, never a pass.
- `E2E_FAIL` needs a reproduction: URL, step, observed vs expected.
