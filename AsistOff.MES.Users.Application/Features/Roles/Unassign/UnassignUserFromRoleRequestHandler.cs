using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Users.Core.Repositories;
using MediatR;

namespace AsistOff.MES.Users.Application.Features.Roles.Unassign;

internal sealed class UnassignUserFromRoleRequestHandler(
    IRolesRepository rolesRepository,
    IUserRolesRepository userRolesRepository)
    : IRequestHandler<UnassignUserFromRoleRequest>
{
    public async Task Handle(UnassignUserFromRoleRequest request, CancellationToken cancellationToken)
    {
        _ = await rolesRepository.GetByIdAsync(request.RoleId, cancellationToken)
            ?? throw new NotFoundException("Role", request.RoleId);

        var assignments = await userRolesRepository.BrowseByUserAsync(request.UserId, cancellationToken);
        var assignment = assignments.SingleOrDefault(x => x.RoleId == request.RoleId)
            ?? throw new NotFoundException("UserRole", request.UserId);

        await userRolesRepository.RemoveAsync(assignment, cancellationToken);
    }
}
