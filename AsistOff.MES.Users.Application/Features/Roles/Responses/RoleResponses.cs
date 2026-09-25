namespace AsistOff.MES.Users.Application.Features.Roles.Responses;

/// <summary>
/// Summary of a tenant role for the browse endpoint. Carries the granted
/// permission codes so the back-office permission matrix can render without
/// one request per role.
/// </summary>
public sealed record RoleResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    IReadOnlyCollection<string> PermissionCodes,
    int MemberCount);

/// <summary>
/// Detailed role view: granted permissions plus assigned members (with
/// e-mail so the membership editor is usable without a users browse endpoint).
/// </summary>
public sealed record RoleDetailResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    IReadOnlyCollection<PermissionResponse> Permissions,
    IReadOnlyCollection<RoleMemberResponse> Members);

/// <summary>
/// Tenant permission as exposed by the permissions browse endpoint.
/// </summary>
public sealed record PermissionResponse(
    Guid Id,
    string Code,
    string Name,
    string Category);

/// <summary>
/// One user assigned to a role.
/// </summary>
public sealed record RoleMemberResponse(
    Guid UserId,
    string Email);
