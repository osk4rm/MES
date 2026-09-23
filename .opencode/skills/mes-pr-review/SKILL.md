---
name: mes-pr-review
description: Use when the mes-reviewer agent audits an AsistOff MES pull request. Provides the review checklist (multi-tenancy, domain rules, tests, frontend conventions) and the required verdict format.
---

# MES pull request review checklist

Review only the diff. Do not trust the implementer's description. For every
finding, cite the file and line.

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
- [ ] UI-facing changes describe a Playwright click-through in the PR body.
- [ ] No existing test disabled, deleted, or weakened.
- [ ] CI-relevant commands would pass (`dotnet build/test`, `npm run build`).

## Verdict

Post the review with:

```
gh pr comment <N> --body "<review>"
```

You cannot approve or request changes on your own PR with a single `gh`
identity, so the verdict is encoded in the comment body. End the body with
exactly one line:

```
VERDICT: APPROVED
```

or

```
VERDICT: CHANGES_REQUESTED
```

`APPROVED` means no blocking issues. Never edit code.
