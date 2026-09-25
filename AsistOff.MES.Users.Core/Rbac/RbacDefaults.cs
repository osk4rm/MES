namespace AsistOff.MES.Users.Core.Rbac;

/// <summary>
/// Data parity for the interim binary authorization model (see ADR-0003).
/// The seeded <c>tenant_admin</c> role keeps the current admin permission set
/// and the <c>user</c> role keeps the current read-only set, so behavior is
/// unchanged in this slice. Follow-up slices materialise these into JWT claims.
/// </summary>
public static class RbacDefaults
{
    public const string AdminRoleCode = "tenant_admin";
    public const string UserRoleCode = "user";

    public const string AdminRoleName = "Tenant Administrator";
    public const string UserRoleName = "User";

    public const string AdminRoleDescription = "Full tenant administration (parity with IsTenantAdmin=true).";
    public const string UserRoleDescription = "Read-only access (parity with IsTenantAdmin=false).";

    /// <summary>
    /// Must stay in sync with <c>SignInRequestHandler.ResolvePermissions</c> admin branch.
    /// </summary>
    public static readonly IReadOnlyList<string> AdminPermissions =
    [
        "users",
        "users.read",
        "users.write",
        "configuration",
        "configuration.read",
        "configuration.write",
        "tenant.admin",
    ];

    /// <summary>
    /// Must stay in sync with <c>SignInRequestHandler.ResolvePermissions</c> non-admin branch.
    /// </summary>
    public static readonly IReadOnlyList<string> UserPermissions =
    [
        "users.read",
        "configuration.read",
    ];

    public static IReadOnlyList<string> AllPermissionCodes =>
        AdminPermissions.Union(UserPermissions).Distinct().ToList();

    public static string CategoryFor(string permissionCode)
    {
        var dot = permissionCode.IndexOf('.');
        return dot <= 0 ? permissionCode : permissionCode[..dot];
    }
}
