using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Users.Application.Features.Roles.Responses;
using AsistOff.MES.Users.Core.Repositories;
using MediatR;

namespace AsistOff.MES.Users.Application.Features.Roles.Get;

internal sealed class GetRoleRequestHandler(
    IRolesRepository rolesRepository,
    IRolePermissionsRepository rolePermissionsRepository,
    IUserRolesRepository userRolesRepository,
    IUsersRepository usersRepository)
    : IRequestHandler<GetRoleRequest, RoleDetailResponse>
{
    public async Task<RoleDetailResponse> Handle(
        GetRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await rolesRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Role", request.Id);

        var links = await rolePermissionsRepository.BrowseByRoleAsync(role.Id, cancellationToken);
        var permissions = links
            .Select(x => x.Permission)
            .Where(x => x is not null)
            .Select(x => new PermissionResponse(x!.Id, x.Code, x.Name, x.Category))
            .OrderBy(x => x.Code, StringComparer.Ordinal)
            .ToList();

        var assignments = await userRolesRepository.BrowseByRoleAsync(role.Id, cancellationToken);
        var members = new List<RoleMemberResponse>(assignments.Count);
        foreach (var assignment in assignments)
        {
            var email = assignment.User?.Email
                ?? (await usersRepository.GetAsync(assignment.UserId))?.Email
                ?? string.Empty;

            members.Add(new RoleMemberResponse(assignment.UserId, email));
        }

        members.Sort((a, b) => string.Compare(a.Email, b.Email, StringComparison.OrdinalIgnoreCase));

        return new RoleDetailResponse(
            role.Id,
            role.Code,
            role.Name,
            role.Description,
            permissions,
            members);
    }
}
