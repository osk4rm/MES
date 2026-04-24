using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ResourceRequirements.Remove;

internal sealed class RemoveResourceRequirementRequestHandler(
    IChildEntitiesRepository childRepository,
    IOperationNodesRepository operationsRepository,
    IRecipeVersionsRepository versionsRepository)
    : IRequestHandler<RemoveResourceRequirementRequest>
{
    public async Task Handle(RemoveResourceRequirementRequest request, CancellationToken cancellationToken)
    {
        var entity = await childRepository.GetResourceRequirementAsync(request.ResourceRequirementId, cancellationToken)
            ?? throw new NotFoundException("ResourceRequirement", request.ResourceRequirementId);

        var op = await operationsRepository.GetAsync(entity.OperationNodeId, cancellationToken)
            ?? throw new NotFoundException("OperationNode", entity.OperationNodeId);

        await VersionGuard.EnsureDraftAsync(versionsRepository, op.RecipeVersionId, cancellationToken);
        await childRepository.RemoveResourceRequirementAsync(entity.Id, cancellationToken);
    }
}
