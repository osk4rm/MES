using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.BomItems.Update;

internal sealed class UpdateBomItemRequestHandler(
    IChildEntitiesRepository childRepository,
    IOperationNodesRepository operationsRepository,
    IRecipeVersionsRepository versionsRepository)
    : IRequestHandler<UpdateBomItemRequest>
{
    public async Task Handle(UpdateBomItemRequest request, CancellationToken cancellationToken)
    {
        var entity = await childRepository.GetBomItemAsync(request.BomItemId, cancellationToken)
            ?? throw new NotFoundException("BomItem", request.BomItemId);

        var op = await operationsRepository.GetAsync(entity.OperationNodeId, cancellationToken)
            ?? throw new NotFoundException("OperationNode", entity.OperationNodeId);

        await VersionGuard.EnsureDraftAsync(versionsRepository, op.RecipeVersionId, cancellationToken);

        entity.ProductId = request.ProductId;
        entity.MeasureUnitId = request.MeasureUnitId;
        entity.Quantity = request.Quantity;
        entity.QuantityType = request.QuantityType;
        entity.ScrapPercentage = request.ScrapPercentage;
        entity.IsOptional = request.IsOptional;
        entity.PreferredWarehouseId = request.PreferredWarehouseId;
        entity.ConsumptionTiming = request.ConsumptionTiming;
        entity.Notes = request.Notes;
        entity.SortIndex = request.SortIndex;

        await childRepository.SaveChangesAsync(cancellationToken);
    }
}
