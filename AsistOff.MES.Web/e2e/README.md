# Committed Playwright smoke suite (issue #272)

One versioned journey proving the core manufacturing flow still works
together: **login (with post-login redirect) → dispatch board (Released
orders) → operator Confirmation (produced + consumed lots) → Lot tree
(genealogy)**.

## Run it (single command)

```powershell
pwsh -File scripts/e2e/smoke.ps1
```

The script starts the stack (`scripts/e2e/app.ps1`, backend `:5243` +
frontend `:5173`), ensures the Playwright chromium browser, runs
`smoke/login-to-lots.spec.ts`, then stops the stack
(`-KeepStack` keeps it running). Database prerequisite is unchanged:
`docker compose up -d postgres` (see `docs/e2e-local-setup.md`).

Once the `e2e-smoke` workflow patch lands, CI runs the same suite in the
`e2e-smoke` job (Postgres service +
`app.ps1 -Action start`) on every PR touching `AsistOff.MES.Web`,
`AsistOff.MES.Production.*`, `scripts/e2e` or the workflow itself, and
publishes traces + screenshots + the HTML report when a check fails.

## Layout

| Path | What |
|---|---|
| `smoke/login-to-lots.spec.ts` | The 4-check journey (serial, one file). Seeds through the API in `beforeAll`, drives the UI, cleans up in `afterAll`. |
| `support/smoke-data.ts` | Pure helpers: per-run `SMK-XXXXXX` tag, code derivation, API payload builders, board/trace parsers. No Playwright imports. |
| `support/smoke-data.spec.ts` | Vitest coverage for those helpers (runs with `npm run test`). |
| `../playwright.config.ts` | Suite config: chromium only, `trace: retain-on-failure`, `screenshot: only-on-failure`. |

## Test-data isolation

Every run mints a unique tag (`buildSmokeTag`) and derives the machine,
recipe, Released order and both lots from it, so repeat runs pass without
manual reset. Cleanup is best-effort: the Confirmation and both lots are
deleted, the Released order / machine / recipe stay as an audit trail.

## Stable selectors

The suite locates pages via `data-testid` hooks (`login-form`,
`dispatch-board`, `order-detail`, `confirmation-form`, `lots-page`,
`lot-genealogy-upstream`, …) plus native `<select>` values (ids, not
labels), so it is immune to `pl`/`en` locale switches. When adding UI,
prefer `data-testid` on plain wrappers over text assertions.
