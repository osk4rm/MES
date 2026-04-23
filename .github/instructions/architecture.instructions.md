---
applyTo: "**/*.cs"
---

# Architecture – Backend (.NET / C#)

## Modular Monolith

The backend is a **modular monolith**. Each domain area is an isolated module with four layers:

```
Module.Core/            # Domain entities, value objects, repository interfaces
Module.Application/     # Use cases (MediatR requests + handlers), request validators
Module.Infrastructure/  # EF Core entity configs, repository implementations, external services
Module.Api/             # Controllers + IModule registration
```

All modules are loaded dynamically at startup via `ModuleLoader` in the Gateway project.

## IModule Contract

Every module exposes an `IModule` implementation that wires it into the host:

```csharp
public interface IModule
{
    string Name { get; }
    string Path { get; }
    IEnumerable<string> Policies { get; }
    void Register(IServiceCollection services, IConfiguration configuration);
    void Use(IApplicationBuilder app);
}
```

## CQRS with MediatR

- All use cases are **MediatR requests**: implement `IRequest<TResponse>`.
- Handlers implement `IRequestHandler<TRequest, TResponse>`.
- Controllers inject `IMediator` and dispatch requests; they hold no business logic.
- Naming convention: `{Action}{Entity}Request` / `{Action}{Entity}RequestHandler`.

```csharp
// Request (command or query)
public record CreateProductRequest(string Code, string Name) : IRequest<Guid>;

// Handler
public class CreateProductRequestHandler : IRequestHandler<CreateProductRequest, Guid>
{
    public async Task<Guid> Handle(CreateProductRequest request, CancellationToken cancellationToken)
    {
        // business logic here
    }
}
```

## Validation

- Each request has a paired validator: `{Action}{Entity}RequestValidator`.
- Validators implement `IRequestValidator<TRequest>` from `AsistOff.MES.Shared.Abstractions.Validation`.
- Validation runs automatically via the `ValidationBehavior<,>` MediatR pipeline behavior registered in `Shared.Infrastructure`.
- Throw `ValidationException` for business rule violations.

## Domain Events

- Events implement `IEvent` from `AsistOff.MES.Shared.Abstractions.Events`.
- Dispatched via `IEventDispatcher`.
- Listeners implement `IEventListener<TEvent>` and are auto-discovered from assemblies.

## Multitenancy

- All tenant-scoped entities implement `ISaasy` (from `AsistOff.MES.Multitenancy.Contracts`):

```csharp
public class Product : IEntity, ISaasy, ISyncable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? SyncId { get; set; }
    // ...
}
```

- Tenant context is resolved via `ITenantContext`.
- `TenantValidationBehavior` (MediatR pipeline behavior) enforces tenant isolation automatically.

## Shared Infrastructure vs Module Infrastructure

| Project | Responsibility |
|---------|---------------|
| `Shared.Infrastructure` | Auth, global EF Core context, messaging, pipeline behaviors, interceptors |
| `Module.Infrastructure` | Entity type configurations, repository implementations specific to a module |

## Pipeline Behaviors (MediatR)

Registered globally in `Shared.Infrastructure.DependencyInjection`:

1. `ValidationBehavior<,>` – runs all `IRequestValidator<TRequest>` for the incoming request.

Registered per module where needed:

2. `TenantValidationBehavior` – validates tenant context for tenant-scoped requests.

## Exception Hierarchy

Throw exceptions from `AsistOff.MES.Shared.Abstractions.Exceptions`:

| Exception | HTTP status |
|-----------|-------------|
| `NotFoundException` | 404 |
| `ValidationException` | 400 |

Global exception handling middleware (`AddExceptionHandling()`) maps these to problem details responses.

## Dependency Injection Conventions

- Each module has a static `DependencyInjection.cs` with extension methods (`Add{Module}()`).
- The Gateway calls `module.Register(services, configuration)` for every loaded module.
- Scoped lifetime is default for repositories and handlers; singleton for providers and options.
