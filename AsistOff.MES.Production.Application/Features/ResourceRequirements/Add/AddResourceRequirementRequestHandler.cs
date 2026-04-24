using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ResourceRequirements.Add;

internal sealed class AddResourceRequirementRequestHandler(
    IChildEntitiesRepository childRepository,
    IOperationNodesRepository operationsRepository,
    IRecipeVersionsRepository versionsRepository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext)
    : IRequestHandler<AddResourceRequirementRequest, Guid>
{
    public async Task<Guid> Handle(AddResourceRequirementRequest request, CancellationToken cancellationToken)
    {
        var op = await operationsRepository.GetAsync(request.OperationId, cancellationToken)
            ?? throw new NotFoundException("OperationNode", request.OperationId);

        await VersionGuard.EnsureDraftAsync(versionsRepository, op.RecipeVersionId, cancellationToken);

        var entity = new ResourceRequirement
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            OperationNodeId = op.Id,
            PreferredDepartmentId = request.PreferredDepartmentId,
            PreferredMachineId = request.PreferredMachineId,
            RequiredCapability = request.RequiredCapability,
            RequiredOperatorCount = request.RequiredOperatorCount <= 0 ? 1 : request.RequiredOperatorCount,
            RequiredRole = request.RequiredRole,
            Notes = request.Notes
        };

        await childRepository.AddResourceRequirementAsync(entity, cancellationToken);
        return entity.Id;
    }
}
