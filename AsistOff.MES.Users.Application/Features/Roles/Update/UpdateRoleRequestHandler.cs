using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Users.Core.Repositories;
using MediatR;

namespace AsistOff.MES.Users.Application.Features.Roles.Update;

internal sealed class UpdateRoleRequestHandler(IRolesRepository rolesRepository)
    : IRequestHandler<UpdateRoleRequest>
{
    public async Task Handle(UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await rolesRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Role", request.Id);

        role.Name = request.Name.Trim();
        role.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        await rolesRepository.UpdateAsync(role, cancellationToken);
    }
}
