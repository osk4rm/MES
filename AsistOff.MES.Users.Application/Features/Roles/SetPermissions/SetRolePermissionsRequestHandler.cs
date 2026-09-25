using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Repositories;
using MediatR;

namespace AsistOff.MES.Users.Application.Features.Roles.SetPermissions;

internal sealed class SetRolePermissionsRequestHandler(
    IRolesRepository rolesRepository,
    IPermissionsRepository permissionsRepository,
    IRolePermissionsRepository rolePermissionsRepository,
    IGuidProvider guidProvider)
    : IRequestHandler<SetRolePermissionsRequest>
{
    public async Task Handle(SetRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        var role = await rolesRepository.GetByIdAsync(request.RoleId, cancellationToken)
            ?? throw new NotFoundException("Role", request.RoleId);

        var desired = request.PermissionIds.Distinct().ToList();
        var permissions = new List<Permission>(desired.Count);
        foreach (var permissionId in desired)
        {
            var permission = await permissionsRepository.GetByIdAsync(permissionId, cancellationToken)
                ?? throw new NotFoundException("Permission", permissionId);

            permissions.Add(permission);
        }

        var current = await rolePermissionsRepository.BrowseByRoleAsync(role.Id, cancellationToken);
        var currentByPermission = current.ToDictionary(x => x.PermissionId);

        var desiredIds = new HashSet<Guid>(desired);

        foreach (var link in current)
        {
            if (!desiredIds.Contains(link.PermissionId))
                await rolePermissionsRepository.RemoveAsync(link, cancellationToken);
        }

        foreach (var permission in permissions)
        {
            if (!currentByPermission.ContainsKey(permission.Id))
            {
                await rolePermissionsRepository.AddAsync(new RolePermission
                {
                    Id = guidProvider.NewGuid(),
                    TenantId = role.TenantId,
                    RoleId = role.Id,
                    PermissionId = permission.Id
                }, cancellationToken);
            }
        }
    }
}
