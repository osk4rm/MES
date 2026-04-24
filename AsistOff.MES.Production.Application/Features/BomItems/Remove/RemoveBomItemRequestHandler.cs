using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.BomItems.Remove;

internal sealed class RemoveBomItemRequestHandler(
    IChildEntitiesRepository childRepository,
    IOperationNodesRepository operationsRepository,
    IRecipeVersionsRepository versionsRepository)
    : IRequestHandler<RemoveBomItemRequest>
{
    public async Task Handle(RemoveBomItemRequest request, CancellationToken cancellationToken)
    {
        var entity = await childRepository.GetBomItemAsync(request.BomItemId, cancellationToken)
            ?? throw new NotFoundException("BomItem", request.BomItemId);

        var op = await operationsRepository.GetAsync(entity.OperationNodeId, cancellationToken)
            ?? throw new NotFoundException("OperationNode", entity.OperationNodeId);

        await VersionGuard.EnsureDraftAsync(versionsRepository, op.RecipeVersionId, cancellationToken);
        await childRepository.RemoveBomItemAsync(entity.Id, cancellationToken);
    }
}
