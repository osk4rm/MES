namespace AsistOff.MES.Shared.Abstractions.Auth;

/// <summary>
/// Provides the permission codes granted to the current caller — the
/// multi-valued <c>permissions</c> JWT claim materialised at sign-in time
/// from the caller's role assignments.
/// </summary>
public interface ICurrentPermissionsAccessor
{
    /// <summary>
    /// Permission codes held by the current caller, or an empty collection
    /// outside an authenticated request (fail closed).
    /// </summary>
    IReadOnlyCollection<string> Permissions { get; }
}
