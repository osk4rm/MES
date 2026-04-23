namespace AsistOff.MES.Shared.Abstractions.Exceptions;

/// <summary>
/// Thrown when a caller fails to authenticate (invalid credentials, inactive
/// tenant, disabled account, etc.). Mapped to HTTP 401 by the global exception
/// handler — semantically distinct from <see cref="ValidationException"/>
/// (400) and <see cref="NotFoundException"/> (404).
/// </summary>
public class AuthenticationException : DomainException
{
    public AuthenticationException(string message) : base(message) { }
    public AuthenticationException(string message, Exception innerException) : base(message, innerException) { }
}
