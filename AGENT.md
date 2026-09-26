# AGENT.md — working agreements for AI coding agents

This file is the short, authoritative set of **do's and don'ts** for any
AI agent (GitHub Copilot cloud agent, Claude Code, Cursor, etc.) contributing
to AsistOff MES. The longer architectural context lives in:

* [`.github/copilot-instructions.md`](.github/copilot-instructions.md) — high‑level rules
* [`.github/instructions/`](.github/instructions/) — area‑specific instructions (backend, frontend, DB, API, testing)
* [`.github/instructions/business-features.instructions.md`](.github/instructions/business-features.instructions.md) — implemented MES business features, workflows and known gaps
* [`docs/adr/`](docs/adr/) — Architecture Decision Records
* [`docs/glossary.md`](docs/glossary.md) — domain vocabulary

**Read those files before making non‑trivial changes.**

---

## Do

* **Keep multi‑tenancy airtight.** Every new MediatR request must implement
  exactly one of:
  * `ITenantRequest` → requires an authenticated tenant (default).
  * `IAllowAnonymousRequest` → explicit opt‑out; must be justified in the PR.
  There is no "implicit anonymous".
* **Every tenant‑scoped entity implements `ISaasy`**. The global EF query
  filter and the `SaasyEntityInterceptor` will then enforce isolation for
  you. Do not write manual `x.TenantId == currentTenant` predicates in
  application handlers — they are redundant.
* **Write tests** for new behavior — a feature is not done with only one kind:
  * **Unit tests** (handlers / validators, no database) live in
    `tests/AsistOff.MES.Shared.Tests/`. If your change touches a new module,
    create a matching `tests/AsistOff.MES.<Module>.Tests/` project.
  * **Endpoint integration tests** live in `tests/AsistOff.MES.Integration.Tests/`
    and drive the real HTTP pipeline (auth → tenant → handler → EF Core) against
    a Testcontainers PostgreSQL via the existing
    `MesApplicationFixture` / `IntegrationTestBase`. Cover the happy path plus
    the failure paths (`401`/`400`/`404`/`409`). Docker must be running.
  * **E2E click-through (Playwright):** for any UI-facing change, run the
    committed login-to-lots smoke suite (`pwsh -File scripts/e2e/smoke.ps1`)
    on the local stack — or click the changed flow through manually when the
    suite does not cover it — before opening the PR, then describe the
    steps/result in the PR body.
* **Prefer the smallest change** that fully solves the task. Unrelated
  cleanups belong in separate PRs.
* **Use the domain glossary.** Say `Production Order`, not "work order" or
  "job". Say `Work Center`, not "workstation".
* **Throw typed domain exceptions** from
  `AsistOff.MES.Shared.Abstractions.Exceptions`:
  * `NotFoundException` → 404
  * `ValidationException` → 400 (business rule / input)
  * `AuthenticationException` → 401 (bad credentials, inactive tenant)
  * `UnauthorizedAccessException` → 401 (missing / invalid tenant)
* **Name async methods with `Async` suffix** and pass through `CancellationToken`.
* **Update documentation** when you change conventions. If you change the
  multi‑tenancy model, update
  [`docs/adr/0002-multi-tenancy-strategy.md`](docs/adr/0002-multi-tenancy-strategy.md)
  in the same PR.

## Don't

* **Don't bypass the tenant query filter** unless you can articulate *why*
  in code comments and the PR description. The only legitimate bypass
  today is `IUsersRepository.GetForAuthenticationAsync` for pre‑auth user
  lookup. Any new bypass needs a review comment from a maintainer.
* **Don't add new hardcoded permissions / tenant names / role strings.**
  Derive them from data. If you need a new permission, extend
  `SignInRequestHandler.ResolvePermissions` (interim) or wait for the RBAC
  model (see [ADR‑0003](docs/adr/0003-auth-jwt-and-rbac.md)).
* **Don't catch `Exception` just to log and rethrow.** It adds noise. Let
  the global exception handler format it.
* **Don't store credentials in `localStorage`** on the frontend. The current
  code does — that's a known debt tracked for the BFF migration. Don't add
  *more* of it.
* **Don't use `any` in TypeScript.** Define a real interface.
* **Don't commit boilerplate from `vite create` / `dotnet new`** (e.g.
  `HelloWorld.vue`, `Class1.cs`, `decode-jwt.js`).
* **Don't touch unrelated tests or migrations** to make your change pass.
  Fix your change instead.

## Before opening a PR

1. `dotnet build AsistOff.MES.sln` — succeeds, no new warnings.
2. `dotnet test AsistOff.MES.sln` — green (includes the Testcontainers
   integration tests; Docker must be running).
3. `cd AsistOff.MES.Web && npm run build` — succeeds (runs `vue-tsc` + Vite).
4. For UI-facing changes: Playwright click-through of the changed flow on the
   local stack (see
   [`testing.instructions.md`](.github/instructions/testing.instructions.md)).
5. Fill in the PR template, tick the **multi‑tenancy checklist** and the
   **testing checklist** (unit + integration).
6. Keep the commit message conventional (`feat:`, `fix:`, `chore:`, `docs:`,
   `refactor:`, `test:`).

## Scenarios

### Adding a new tenant‑scoped entity

1. Define the entity in `*.Core/Entities/<Name>.cs`, implement `ISaasy`,
   `IEntity`, optionally `IAuditable`.
2. Add an `IEntityConfigurator` in `*.Infrastructure/Configurations/` that
   configures the table, indexes, and at least a `(TenantId, Code)` unique
   constraint if the entity has a code.
3. Add an EF Core migration
   (`dotnet ef migrations add <Name> --project AsistOff.MES.Shared.Infrastructure --startup-project AsistOff.MES.Gateway`).
4. Add a repository interface in `*.Core/Repositories/`, implementation in
   `*.Infrastructure/Repositories/`.
5. Add Application handlers for CRUD + browse, each implementing
   `ITenantRequest<TResponse>` (or `IRequest<TResponse>` with `ITenantRequest` if no return value).
6. Add a controller in `*.Api/Controllers/`.
7. Add a unit test for at least the Create and Browse handlers.
8. Add an endpoint integration test class in
   `tests/AsistOff.MES.Integration.Tests/Endpoints/<Feature>EndpointTests.cs`
   (extend `IntegrationTestBase`) covering create + read + the failure paths.

### Adding an endpoint to an existing feature

1. Add the MediatR request/handler (+ validator) following the module layout.
2. Add the controller action.
3. Add unit tests in `tests/AsistOff.MES.Shared.Tests/`.
4. Add/extend an endpoint integration test in
   `tests/AsistOff.MES.Integration.Tests/` asserting the HTTP contract.
5. If the UI consumes the endpoint, do the Playwright click-through on the
   local stack and record it in the PR.

### Adding an anonymous endpoint (e.g. sign‑up)

1. Mark the request with `IAllowAnonymousRequest`.
2. Mark the controller action with `[AllowAnonymous]`.
3. Justify in the PR description why no tenant is required.
4. Do not query tenant‑scoped entities without an explicit tenant id
   parameter (the global filter will hide them from you anyway).
