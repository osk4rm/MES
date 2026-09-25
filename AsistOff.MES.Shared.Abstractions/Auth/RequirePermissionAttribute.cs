namespace AsistOff.MES.Shared.Abstractions.Auth;

/// <summary>
/// Declares that a MediatR request requires the caller to hold a permission
/// code (e.g. <c>configuration.write</c>). Enforced by the
/// <c>AuthorizationBehavior</c> pipeline step: a request carrying this
/// attribute fails with <see cref="Exceptions.ForbiddenException"/> (HTTP 403)
/// when the caller token lacks any of the declared permissions.
/// Requests without this attribute keep existing behavior unchanged.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : Attribute
{
    public RequirePermissionAttribute(string permission)
    {
        Permission = permission;
    }

    public string Permission { get; }
}
