using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Users.Application.Features.Roles.Responses;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Repositories;
using MediatR;

namespace AsistOff.MES.Users.Application.Features.Roles.Create;

internal sealed class CreateRoleRequestHandler(
    IRolesRepository rolesRepository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext)
    : IRequestHandler<CreateRoleRequest, RoleResponse>
{
    public async Task<RoleResponse> Handle(
        CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();
        var existing = await rolesRepository.GetByCodeAsync(code, cancellationToken);
        if (existing is not null)
            throw new ConflictException($"Role with code '{code}' already exists.");

        var role = new Role
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            Code = code,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim()
        };

        await rolesRepository.AddAsync(role, cancellationToken);

        return new RoleResponse(role.Id, role.Code, role.Name, role.Description, [], 0);
    }
}
