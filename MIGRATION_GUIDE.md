# ErrorOr to Exception Migration - Setup Guide

## Overview
Your application has been successfully migrated from ErrorOr to a custom exception-based approach with global exception handling.

## What Was Changed

### 1. Custom Exception Classes Created
- `DomainException`: Base abstract exception class
- `ValidationException`: For validation errors (replaces ErrorType.Validation)
- `ConflictException`: For conflict errors (replaces ErrorType.Conflict)
- `NotFoundException`: For not found errors (replaces ErrorType.NotFound)
- `RepositoryException`: For repository/database errors

### 2. Global Exception Handler
- `GlobalExceptionHandler`: Converts exceptions to proper HTTP responses with Problem Details
- `ExceptionHandlingExtensions`: DI registration helper

### 3. Files Updated
- Removed ErrorOr package references from project files
- Updated all request handlers to throw exceptions instead of returning ErrorOr
- Updated ValidationBehavior to throw ValidationException
- Simplified ApiController (no longer needs ErrorOr handling)
- Updated all request/response types to remove ErrorOr wrappers

## Setup Instructions

### 1. Register Global Exception Handler

In your `Program.cs` or startup configuration, add the exception handling:

```csharp
using AsistOff.MES.Shared.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add other services...
builder.Services.AddExceptionHandling();

var app = builder.Build();

// Add exception handling middleware (MUST be early in pipeline)
app.UseExceptionHandler();

// Add other middleware...
app.Run();
```

### 2. Remove ErrorOr Package References

The following packages have been removed from project files:
- AsistOff.MES.Users.Application.csproj
- AsistOff.MES.Shared.Abstractions.csproj

Run the following commands to clean up any remaining package references:

```bash
dotnet remove package ErrorOr --interactive
```

### 3. Update Any Controllers

Your controllers are now simplified. Instead of handling ErrorOr results, they can focus on business logic:

```csharp
[ApiController]
public class UsersController : ApiController
{
    [HttpPost]
    public async Task<IActionResult> CreateUser(CreateUserRequest request)
    {
        // No need to handle ErrorOr - exceptions are handled globally
        await _mediator.Send(request);
        return Ok();
    }

    [HttpPost("sign-in")]
    public async Task<IActionResult> SignIn(SignInRequest request)
    {
        // Returns JsonWebToken directly, exceptions handled globally
        var token = await _mediator.Send(request);
        return Ok(token);
    }
}
```

### 4. Exception Handling Examples

#### Throwing Exceptions in Handlers:
```csharp
public async Task<Unit> Handle(CreateUserRequest request, CancellationToken cancellationToken)
{
    var existingUser = await _usersRepository.GetAsync(request.Email);
    if (existingUser is not null)
    {
        throw new ConflictException("User with this email already exists");
    }
    
    // Continue with creation...
    return Unit.Value;
}
```

#### Validation Exceptions:
```csharp
// Single validation error
throw new ValidationException("email", "Email is required");

// Multiple validation errors
var errors = new Dictionary<string, string[]>
{
    { "email", new[] { "Email is required", "Email format is invalid" } },
    { "password", new[] { "Password is too short" } }
};
throw new ValidationException(errors);
```

## HTTP Response Examples

### Validation Error (400 Bad Request)
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "email": ["Email is required"],
    "password": ["Password is too short"]
  }
}
```

### Not Found Error (404 Not Found)
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "User with key '123' was not found."
}
```

### Conflict Error (409 Conflict)
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.8",
  "title": "Conflict",
  "status": 409,
  "detail": "User with this email already exists"
}
```

## Benefits of This Approach

1. **Cleaner Code**: No more ErrorOr wrapper types cluttering your code
2. **Standard Exception Handling**: Uses .NET's built-in exception handling patterns
3. **Global Consistency**: All exceptions handled uniformly across the application
4. **Better Performance**: No need to wrap/unwrap results in ErrorOr containers
5. **Simplified Controllers**: Controllers focus on business logic, not error handling
6. **Type Safety**: Direct return types instead of ErrorOr wrappers

## Migration Complete

Your application has been successfully migrated from ErrorOr to exception-based error handling. The global exception handler will automatically convert exceptions to appropriate HTTP responses with proper status codes and Problem Details format.

Make sure to test your endpoints to ensure the exception handling works as expected!
