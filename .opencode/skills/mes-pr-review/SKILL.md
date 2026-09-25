---
name: mes-pr-review
description: Use when the mes-reviewer agent audits an AsistOff MES pull request. Provides the review checklist (multi-tenancy, domain rules, tests, frontend conventions) and the required verdict format.
---

# MES pull request review checklist

Review only the diff. Do not trust the implementer's description. For every
finding, cite the file and line.

Keep the verdict a function of the diff, not of the round. The same diff must
get the same verdict every time it is reviewed: a gate that flips between
rounds on identical input cannot converge, and this swarm runs fix rounds
without a cap. If a point is genuinely arguable, put it under "Notes,
non-blocking" and approve. Reserve `CHANGES_REQUESTED` for defects you can
cite in the diff.

## 1. Multi-tenancy (blocking)

- [ ] Every new MediatR request implements exactly one of `ITenantRequest` or
      `IAllowAnonymousRequest`.
- [ ] Anonymous requests are justified in the PR body.
- [ ] Every tenant-scoped entity implements `ISaasy`.
- [ ] No manual `TenantId == currentTenant` predicates in handlers.
- [ ] No new bypass of the global tenant query filter (the only sanctioned
      bypass today is `IUsersRepository.GetForAuthenticationAsync`).

## 2. Domain and architecture

- [ ] Typed exceptions from `AsistOff.MES.Shared.Abstractions.Exceptions`.
- [ ] Async methods use the `Async` suffix and pass `CancellationToken`.
- [ ] No new hardcoded permissions / tenant names / role strings.
- [ ] Handler / controller / repository structure matches module conventions.
- [ ] No unrelated refactors; no changes to unrelated tests or migrations.

## 3. Frontend

- [ ] No `any` in TypeScript; real interfaces defined.
- [ ] Every SFC uses `<script setup lang="ts">`.
- [ ] No credentials added to `localStorage`.

## 4. Tests

- [ ] New behaviour has **both** meaningful unit tests **and** endpoint
      integration tests in `tests/AsistOff.MES.Integration.Tests/` (HTTP call,
      status code + body + persistence - not only a smoke test).
- [ ] Failure paths (`401`/`400`/`404`/`409`) covered where relevant.
- [ ] No existing test disabled, deleted, or weakened.
- [ ] CI-relevant commands would pass (`dotnet build/test`, `npm run build`).

### Not yours to block on

These belong to other stages. Report them as observations; never let them
produce `CHANGES_REQUESTED`, or the fix round cannot satisfy them:

- **The Playwright click-through.** The CI agent runner has no application,
  database or browser, and the fixer is explicitly forbidden from starting one.
  AGENT.md defines this click-through as a *manual* verification step. The
  `mes-e2e-tester` stage runs the browser smoke on a real stack; a human
  performs the manual pass before merge. A PR that documents an unrun browser
  check honestly ("not re-run in this pass, e2e stage owns it") is correct —
  do not ask for it twice.
- **Whether the PR description is worded the way you would word it.** Only
  block on a description that is *contradicted* by the diff or states something
  untrue (e.g. claims a test exists that does not).
- **Test adequacy.** `mes-verifier` owns whether the tests prove the acceptance
  criteria. You check that tests exist, assert real behaviour, and were not
  weakened — leave criterion coverage to the verifier.

## Verdict

Post the review with:

```
gh pr comment <N> --body "<review>"
```

You cannot approve or request changes on your own PR with a single `gh`
identity, so the verdict is encoded in the comment body. End the body with
exactly one verdict on its own line:

```
VERDICT: APPROVED
```

or

```
VERDICT: CHANGES_REQUESTED
```

Write no other `VERDICT:` line anywhere (do not quote the alternative —
the dispatcher treats multiple distinct verdicts as `AMBIGUOUS` and escalates
to a human). `APPROVED` means no blocking issues. Never edit code.
