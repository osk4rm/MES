namespace AsistOff.MES.Shared.Abstractions.Exceptions;

/// <summary>
/// Thrown when an authenticated caller lacks a permission required by the
/// requested operation. Mapped to HTTP 403 by the global exception handler —
/// semantically distinct from <see cref="AuthenticationException"/> (401,
/// caller is not authenticated) and <see cref="ValidationException"/> (400).
/// </summary>
public class ForbiddenException : DomainException
{
    public ForbiddenException(string message) : base(message) { }
    public ForbiddenException(string message, Exception innerException) : base(message, innerException) { }
}
