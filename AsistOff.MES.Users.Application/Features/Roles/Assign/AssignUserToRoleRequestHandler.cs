using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Repositories;
using MediatR;

namespace AsistOff.MES.Users.Application.Features.Roles.Assign;

internal sealed class AssignUserToRoleRequestHandler(
    IRolesRepository rolesRepository,
    IUsersRepository usersRepository,
    IUserRolesRepository userRolesRepository,
    IGuidProvider guidProvider)
    : IRequestHandler<AssignUserToRoleRequest>
{
    public async Task Handle(AssignUserToRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await rolesRepository.GetByIdAsync(request.RoleId, cancellationToken)
            ?? throw new NotFoundException("Role", request.RoleId);

        var user = await usersRepository.GetAsync(request.UserId)
            ?? throw new NotFoundException("User", request.UserId);

        var assignments = await userRolesRepository.BrowseByUserAsync(user.Id, cancellationToken);
        if (assignments.Any(x => x.RoleId == role.Id))
            return;

        await userRolesRepository.AddAsync(new UserRole
        {
            Id = guidProvider.NewGuid(),
            TenantId = role.TenantId,
            UserId = user.Id,
            RoleId = role.Id
        }, cancellationToken);
    }
}
