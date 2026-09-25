namespace AsistOff.MES.Shared.Abstractions.Auth;

/// <summary>
/// Declares that a MediatR request requires the caller to hold a permission
/// code (e.g. <c>configuration.write</c>). Enforced by the
/// <c>AuthorizationBehavior</c> pipeline step: a request carrying this
/// attribute fails with <see cref="Exceptions.ForbiddenException"/> (HTTP 403)
/// when the caller token lacks any of the declared permissions.
/// Requests without this attribute must be on the documented
/// <see cref="AuthorizationAllowlist"/> or in a legacy pass-through assembly;
/// otherwise they are rejected with <see cref="Exceptions.ForbiddenException"/>
/// (default-deny, issue #231). Permission codes must be
/// <c>RbacDefaults</c> constants — no string literals.
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
