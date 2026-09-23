---
applyTo: "**/*.Tests/**,**/*Tests.cs,**/*Spec.cs,**/*.test.ts,**/*.spec.ts,**/__tests__/**"
---

# Testing Conventions

## Current State

| Project | Kind | What it covers |
|---------|------|----------------|
| `tests/AsistOff.MES.Shared.Tests` | Unit (xUnit + Moq) | Handlers, validators, interceptors, behaviors - no database. |
| `tests/AsistOff.MES.Integration.Tests` | Endpoint integration (xUnit + `WebApplicationFactory` + Testcontainers PostgreSQL) | The real HTTP pipeline for a single endpoint, against a real PostgreSQL. |

## Definition of done (every feature / endpoint)

A new feature is **not done** until it ships with **both**:

1. **Unit tests** for the new application logic (handlers / validators) in
   `tests/AsistOff.MES.Shared.Tests` (or a matching module test project).
2. **Endpoint integration test(s)** in `tests/AsistOff.MES.Integration.Tests`
   that call the new endpoint over HTTP and assert the observable contract
   (status code + response body + persistence). One test class per endpoint
   (`<Feature>EndpointTests`).

Plus, for UI-facing changes, the agent must **click through the change with
Playwright on the local stack** (see *End-to-end (Playwright)* below) before
opening the PR. There is no committed Playwright suite yet - the click-through
is a manual verification step that must be described in the PR body.

## Backend – xUnit (.NET)

### Project Setup

- Test project naming: `{Module}.{Layer}.Tests` (e.g. `AsistOff.MES.Users.Application.Tests`).
- Add test projects to the solution file (`AsistOff.MES.sln`).
- NuGet packages to use: `xunit`, `xunit.runner.visualstudio`, `Moq` (or `NSubstitute`), `FluentAssertions`.

### Unit Tests

- Use the **Arrange / Act / Assert** pattern with a blank line between each section.
- Test one behaviour per test method.
- Name tests: `{Method}_{Scenario}_{ExpectedResult}` (e.g. `Handle_UserNotFound_ThrowsNotFoundException`).
- Mock repository interfaces from `Module.Core/Repositories/`; do not use real EF Core contexts in unit tests.

```csharp
[Fact]
public async Task Handle_UserNotFound_ThrowsNotFoundException()
{
    // Arrange
    var repo = new Mock<IUsersRepository>();
    repo.Setup(r => r.GetAsync(It.IsAny<string>(), default)).ReturnsAsync((User?)null);
    var handler = new SignInRequestHandler(repo.Object, Mock.Of<IPasswordHasher<User>>(), Mock.Of<IAuthManager>());

    // Act
    var act = () => handler.Handle(new SignInRequest("x@x.com", "pw"), default);

    // Assert
    await act.Should().ThrowAsync<NotFoundException>();
}
```

### Validator Tests

- Each `IRequestValidator<T>` should have dedicated tests covering valid and invalid inputs.
- Test every validation rule in isolation.

### Integration Tests (endpoint scope)

Integration tests live in `tests/AsistOff.MES.Integration.Tests` and prove that a
single endpoint works end-to-end through the **real** HTTP pipeline: routing,
JWT authentication, tenant resolution, validation behavior, MediatR handler,
EF Core persistence and the global exception handler.

The harness is already in place - reuse it, do not reinvent it:

| File | Responsibility |
|------|----------------|
| `Infrastructure/MesApplicationFixture.cs` | xUnit collection fixture shared by all integration tests: starts the `postgres:16-alpine` Testcontainer once, boots the host eagerly (migrations + dev seeder run), exposes `CreateClient()`, `CreateAuthenticatedClientAsync(...)` and `CreateTenantAsync()`. |
| `Infrastructure/IntegrationCollection.cs` | `[CollectionDefinition("Integration", DisableParallelization = true)]` - one container per run, tests run sequentially. |
| `Infrastructure/MesWebApplicationFactory.cs` | `WebApplicationFactory<Program>` that only repoints both EF Core contexts (`DefaultContext`, `MultitenancyDbContext`) at the container - everything else is the production configuration. |
| `Endpoints/IntegrationTestBase.cs` | Base class + JSON helpers; test classes add `[Collection(IntegrationCollection.Name)]`. |
| `TestData/IntegrationTestData.cs` | Seeded dev tenant credentials (`admin@dev.local` / `Passw0rd!`). |
| `TestData/ApiContracts.cs` | DTOs mirroring the JSON returned by the endpoints under test. |

Rules:

- **No mocks** in integration tests - exercise the real host and a real database.
- Authenticate through the API (`CreateAuthenticatedClientAsync()` calls
  `POST /api/auth/sign-in`), never by fabricating a token or disabling auth.
- Assert the **HTTP contract**: status code, response body, and (when the
  endpoint persists) a follow-up GET. Also cover the failure paths you own -
  `401` without a token, `400` validation, `404`, `409`.
- Keep tests independent: use unique data (e.g. GUID-based codes); **all**
  integration tests share one tenant/database via the collection fixture.
- Docker is required. Locally: `dotnet test tests/AsistOff.MES.Integration.Tests`.
  CI runs the same command via `dotnet test AsistOff.MES.sln`.

Skeleton:

```csharp
[Collection(IntegrationCollection.Name)]
public sealed class WidgetsEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/widgets";

    [Fact]
    public async Task Create_ReturnsCreated_AndIsRetrievableById()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = $"IT-{Guid.NewGuid():N}"[..12];

        // Act
        var create = await client.PostAsJsonAsync(BaseUrl, new { code, name = "Widget" });

        // Assert
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<WidgetDto>(create);

        var get = await client.GetAsync($"{BaseUrl}/{created.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

> Unit tests still use the lightweight EF Core **InMemory** provider; only the
> integration project uses Testcontainers.

## Frontend – Vitest

### Setup

- Vitest is compatible with the existing Vite config – add it as a dev dependency.
- Use `@vue/test-utils` for component tests.
- Test files: `*.spec.ts` (preferred) or `*.test.ts`, co-located with the source file or under `__tests__/`.

### Unit Tests (composables / services)

- Test composable logic by calling the function directly (no component wrapper needed).
- Mock Axios requests using `vi.mock('axios')` or `msw`.

### Component Tests

- Use `mount` / `shallowMount` from `@vue/test-utils`.
- Mock Pinia stores with `createTestingPinia` from `@pinia/testing`.
- Assert rendered output and emitted events; avoid testing implementation details.

```typescript
import { mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import MyComponent from './MyComponent.vue'

it('shows the title', () => {
  const wrapper = mount(MyComponent, {
    props: { title: 'Hello' },
    global: { plugins: [createTestingPinia()] },
  })
  expect(wrapper.text()).toContain('Hello')
})
```

## End-to-end (Playwright) – required click-through

There is **no committed Playwright suite yet**. For any change that touches the
UI (new view, form, flow, or a backend endpoint the UI consumes), the agent
must **run the local stack and click the change through with Playwright**
before opening the PR, then record the result in the PR body.

Local stack:

```powershell
# terminal 1 - backend (requires a local PostgreSQL, see user secrets / docker)
dotnet run --project AsistOff.MES.Gateway

# terminal 2 - frontend
cd AsistOff.MES.Web; npm run dev
```

Then, using the Playwright browser tooling:

1. Navigate to the frontend dev server (default `http://localhost:5173`).
2. Sign in with a seeded dev tenant admin (`admin@dev.local` / `Passw0rd!`).
3. Perform the changed user journey (create / edit / filter / navigate).
4. Assert the visible result (table row, toast, validation message) and check
   the browser console for errors.
5. Paste a short summary of the steps and the outcome into the PR's
   *Manual test steps* section.

Rules:

- Do not weaken or skip the click-through to make the PR look green.
- If the flow cannot be exercised locally, say so explicitly in the PR and
  explain why.
- When a committed Playwright suite is introduced later, these steps become
  automated specs and this section will be updated.

## General Rules

- **Every new feature ships with unit tests *and* endpoint integration tests**
  (see *Definition of done*). Reviewers reject PRs missing either.
- Do **not** test EF Core configurations, migration files, or generated code.
- Do **not** remove or disable existing tests to make your code compile – fix the underlying issue.
- Keep tests fast: avoid Thread.Sleep / real timers; use fakes or virtual clocks.
- CI should run all tests; ensure new test projects are included in the build pipeline.
- Integration tests need Docker; the CI `backend` job already provides it.
