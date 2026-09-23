namespace AsistOff.MES.Shared.Abstractions.Auth;

/// <summary>
/// Provides the identifier of the currently authenticated user, if any.
/// </summary>
public interface ICurrentUserAccessor
{
    Guid? UserId { get; }
}
