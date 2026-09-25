namespace AsistOff.MES.Multitenancy.Contracts;

/// <summary>
/// Minimal public projection returned by anonymous tenant self-registration
/// (<c>POST /api/tenants</c>). Deliberately contains only the tenant identity
/// (<see cref="Id"/>), the public <see cref="Name"/> and the
/// <see cref="IsActive"/> flag — no contact e-mail, settings, connection
/// strings, tokens, secrets or internal flags are ever exposed to the
/// unauthenticated caller.
/// </summary>
public record AnonymousTenantResponse(
    Guid Id,
    string Name,
    bool IsActive);
