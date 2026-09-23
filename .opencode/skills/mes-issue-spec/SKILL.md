---
name: mes-issue-spec
description: Use when the mes-analyst agent drafts a new AsistOff MES issue. Provides the required issue structure (context, scope, acceptance criteria, multi-tenancy impact, test plan) and the labelling rule.
---

# MES issue specification template

Every issue created by `mes-analyst` must follow this structure so that
`mes-implementer` can act on it without further questions. `mes-researcher` does
**not** create issues — it only appends `gap` rows to `docs/feature-tracker.md`.

## Title

`<area>: <imperative outcome>`

Examples:

- `production: add downtime reason codes to operator confirmations`
- `config: expose work-center calendars`
- `api: support EAN lookup on the shopfloor endpoint`

## Body

### Context

Why this matters for a manufacturing customer. Tie it to the glossary domain.

### Glossary terms

List the `docs/glossary.md` terms this issue uses (e.g. Production Order,
Work Center, Downtime, Reason code) so terminology stays consistent.

### Scope

- **In scope:** concrete deliverables.
- **Out of scope:** explicitly excluded adjacent work.

### Acceptance criteria

A checklist of objectively verifiable, testable statements:

- [ ] `...`
- [ ] `...`

### Multi-tenancy impact

- New tenant-scoped entities? If yes, they must implement `ISaasy`.
- New MediatR requests? State whether each is `ITenantRequest` or
  `IAllowAnonymousRequest` (anonymous requires justification).

### Test plan

Which tests prove each acceptance criterion. Every feature requires **both**:

- **unit tests** (`tests/AsistOff.MES.Shared.Tests/`) for handlers/validators, and
- **endpoint integration tests** (`tests/AsistOff.MES.Integration.Tests/`,
  Testcontainers PostgreSQL, extend `IntegrationTestBase`) covering the happy
  path and the relevant failure paths.

UI-facing changes additionally require a **Playwright click-through** of the
changed flow on the local stack (recorded in the PR). Follow
`.github/instructions/testing.instructions.md`.

### Affected areas

Backend module(s), API controller(s), frontend view(s), and whether an EF Core
migration is required.

## Labelling

- `mes-analyst` creates the issue and adds the `ai:implement` label so the
  orchestrator picks it up. It may instead complete and label an existing
  unlabeled proposal issue.
- `mes-researcher` never creates issues — it only appends new `gap` rows to
  `docs/feature-tracker.md`.
