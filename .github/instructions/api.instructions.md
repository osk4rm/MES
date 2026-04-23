---
applyTo: "**/Controllers/**,**/*.Api/**,**/Api/**"
---

# API – Controllers & Endpoints

## Controller Conventions

- Inherit from `ControllerBase` (not `Controller` – no Razor views).
- Decorate with `[ApiController]` and `[Route("api/{module}/{controller}")]`.
- Inject `IMediator` – dispatch all work through MediatR; **no business logic in controllers**.
- One action per HTTP verb/route combination; keep actions concise.

```csharp
[ApiController]
[Route("api/configuration/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductsController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<IActionResult> Create(CreateProductRequest request, CancellationToken ct)
        => Ok(await _mediator.Send(request, ct));
}
```

## Response Conventions

| Scenario | Return |
|----------|--------|
| Success with body | `return Ok(result);` (200) |
| Resource created | `return StatusCode(StatusCodes.Status201Created, result);` |
| No body | `return NoContent();` (204) |
| Not found | Throw `NotFoundException` – handled globally |
| Bad input | Throw `ValidationException` – handled globally |

**Never catch exceptions in controllers.** Global error handling (registered via `AddExceptionHandling()`) maps domain exceptions to RFC 7807 problem details responses.

## Error Handling

Global middleware maps exceptions automatically:

| Exception class | HTTP status |
|----------------|-------------|
| `NotFoundException` | 404 Not Found |
| `ValidationException` | 400 Bad Request |
| `ForbiddenException` | 403 Forbidden |
| Unhandled `Exception` | 500 Internal Server Error |

All exception types live in `AsistOff.MES.Shared.Abstractions.Exceptions`.

## Authorization

- Use `[Authorize(Policy = "policyName")]` – policies are declared in `IModule.Policies` and registered at startup.
- Available JWT claims:

| Claim | Description |
|-------|-------------|
| `permissions` | Array of permission strings (e.g. `"users"`, `"users.read"`, `"configuration"`) |
| `tenant_id` | Current tenant GUID |
| `tenant_name` | Human-readable tenant name |
| `tenant_active` | Whether the tenant is active (`"true"` / `"false"`) |

- Anonymous endpoints use `[AllowAnonymous]` explicitly.

## Module Registration

Controllers are wired into the app via the module's `IModule.Use()` call. The module's `IModule.Register()` also sets up any Swagger operation filters or policy registrations needed by the controllers.

## Swagger / OpenAPI

- Swagger is auto-configured by `AddPresentation()` in the Gateway.
- Available at `/swagger` in Development.
- Add XML doc comments on action methods and request types to improve Swagger descriptions.
- Endpoint: `GET /swagger/v1/swagger.json`

## Request Binding

- `[FromBody]` is the default for `[HttpPost]` / `[HttpPut]` (set by `[ApiController]`).
- Use `[FromRoute]` for id parameters: `[HttpGet("{id:guid}")]`.
- Use `[FromQuery]` for pagination and filter parameters.

## Pagination

Use the `Pagination` types from `AsistOff.MES.Shared.Abstractions.Pagination` for paginated list endpoints. Return a paged result wrapper instead of a raw list.
