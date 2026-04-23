---
applyTo: "**/*.Tests/**,**/*Tests.cs,**/*Spec.cs,**/*.test.ts,**/*.spec.ts,**/__tests__/**"
---

# Testing Conventions

## Current State

There are **no test projects** in the solution yet. Use the guidelines below when adding tests.

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

### Integration Tests

- Use **Testcontainers** (`Testcontainers.PostgreSql`) to spin up a real PostgreSQL instance.
- Apply migrations at test startup using the same `ApplyAllPendingMigrations()` extension.
- Wrap each test in a transaction and roll back after the test to keep the database clean.

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

## General Rules

- Do **not** test EF Core configurations, migration files, or generated code.
- Do **not** remove or disable existing tests to make your code compile – fix the underlying issue.
- Keep tests fast: avoid Thread.Sleep / real timers; use fakes or virtual clocks.
- CI should run all tests; ensure new test projects are included in the build pipeline.
