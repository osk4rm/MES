## Summary

<!-- What does this PR do? Link the related issue/ADR. -->

## Changes

<!-- Bullet the concrete changes made. -->

## Type of change

- [ ] Bug fix
- [ ] New feature
- [ ] Refactor (no behavioral change)
- [ ] Documentation
- [ ] Chore / tooling

## Multi‑tenancy checklist (delete if not applicable)

- [ ] All new MediatR requests implement `ITenantRequest` **or** `IAllowAnonymousRequest` (with justification)
- [ ] All new entities with tenant scope implement `ISaasy`
- [ ] Any new `DbSet<T>` query that bypasses the tenant filter is explicitly commented and justified
- [ ] No manual `TenantId ==` predicates added in application layer (the EF global filter handles this)

## Testing

- [ ] Unit tests added / updated (`tests/AsistOff.MES.Shared.Tests`)
- [ ] Endpoint integration tests added / updated (`tests/AsistOff.MES.Integration.Tests`, Testcontainers PostgreSQL)
- [ ] Manual test steps (Playwright click-through for UI-facing changes):
  1. …

## Screenshots / screencasts

<!-- For UI changes. -->

## Notes for reviewers

<!-- Known limitations, follow‑ups, decisions made. -->
