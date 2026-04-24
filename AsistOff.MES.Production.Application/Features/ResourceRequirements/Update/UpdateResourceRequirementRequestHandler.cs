using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ResourceRequirements.Update;

internal sealed class UpdateResourceRequirementRequestHandler(
    IChildEntitiesRepository childRepository,
    IOperationNodesRepository operationsRepository,
    IRecipeVersionsRepository versionsRepository)
    : IRequestHandler<UpdateResourceRequirementRequest>
{
    public async Task Handle(UpdateResourceRequirementRequest request, CancellationToken cancellationToken)
    {
        var entity = await childRepository.GetResourceRequirementAsync(request.ResourceRequirementId, cancellationToken)
            ?? throw new NotFoundException("ResourceRequirement", request.ResourceRequirementId);

        var op = await operationsRepository.GetAsync(entity.OperationNodeId, cancellationToken)
            ?? throw new NotFoundException("OperationNode", entity.OperationNodeId);

        await VersionGuard.EnsureDraftAsync(versionsRepository, op.RecipeVersionId, cancellationToken);

        entity.PreferredDepartmentId = request.PreferredDepartmentId;
        entity.PreferredMachineId = request.PreferredMachineId;
        entity.RequiredCapability = request.RequiredCapability;
        entity.RequiredOperatorCount = request.RequiredOperatorCount <= 0 ? 1 : request.RequiredOperatorCount;
        entity.RequiredRole = request.RequiredRole;
        entity.Notes = request.Notes;

        await childRepository.SaveChangesAsync(cancellationToken);
    }
}
