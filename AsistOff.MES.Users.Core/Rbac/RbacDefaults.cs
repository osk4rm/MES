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

    // Permission codes. All RequirePermission attributes and the database seed
    // must reference these constants — no permission string literals elsewhere.
    public const string Users = "users";
    public const string UsersRead = "users.read";
    public const string UsersWrite = "users.write";
    public const string Configuration = "configuration";
    public const string ConfigurationRead = "configuration.read";
    public const string ConfigurationWrite = "configuration.write";
    public const string TenantAdmin = "tenant.admin";

    /// <summary>
    /// Must stay in sync with <c>SignInRequestHandler.ResolvePermissions</c> admin branch.
    /// </summary>
    public static readonly IReadOnlyList<string> AdminPermissions =
    [
        Users,
        UsersRead,
        UsersWrite,
        Configuration,
        ConfigurationRead,
        ConfigurationWrite,
        TenantAdmin,
    ];

    /// <summary>
    /// Must stay in sync with <c>SignInRequestHandler.ResolvePermissions</c> non-admin branch.
    /// </summary>
    public static readonly IReadOnlyList<string> UserPermissions =
    [
        UsersRead,
        ConfigurationRead,
    ];

    public static IReadOnlyList<string> AllPermissionCodes =>
        AdminPermissions.Union(UserPermissions).Distinct().ToList();

    public static string CategoryFor(string permissionCode)
    {
        var dot = permissionCode.IndexOf('.');
        return dot <= 0 ? permissionCode : permissionCode[..dot];
    }
}
