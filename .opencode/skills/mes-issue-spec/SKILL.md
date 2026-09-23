---
name: mes-issue-spec
description: Use when the mes-analyst or mes-researcher agent drafts a new AsistOff MES issue. Provides the required issue structure (context, scope, acceptance criteria, multi-tenancy impact, test plan) and the labelling rule.
---

# MES issue specification template

Every issue created by `mes-analyst` or `mes-researcher` must follow this
structure so that `mes-implementer` can act on it without further questions.

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

Which unit / integration tests prove each acceptance criterion. Follow
`.github/instructions/testing.instructions.md`.

### Affected areas

Backend module(s), API controller(s), frontend view(s), and whether an EF Core
migration is required.

## Labelling

- `mes-analyst` output: create the issue and add the `ai:implement` label so
  the orchestrator picks it up.
- `mes-researcher` output: create the issue **without** `ai:implement` — it is a
  proposal that a human or the analyst promotes.
