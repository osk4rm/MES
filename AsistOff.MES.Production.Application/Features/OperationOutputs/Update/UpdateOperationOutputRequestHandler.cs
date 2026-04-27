using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.OperationOutputs.Update;

internal sealed class UpdateOperationOutputRequestHandler(
    IChildEntitiesRepository childRepository,
    IOperationNodesRepository operationsRepository,
    IRecipeVersionsRepository versionsRepository)
    : IRequestHandler<UpdateOperationOutputRequest>
{
    public async Task Handle(UpdateOperationOutputRequest request, CancellationToken cancellationToken)
    {
        var entity = await childRepository.GetOperationOutputAsync(request.OutputId, cancellationToken)
            ?? throw new NotFoundException("OperationOutput", request.OutputId);

        var op = await operationsRepository.GetAsync(entity.OperationNodeId, cancellationToken)
            ?? throw new NotFoundException("OperationNode", entity.OperationNodeId);

        await VersionGuard.EnsureDraftAsync(versionsRepository, op.RecipeVersionId, cancellationToken);

        entity.ProductId = request.ProductId;
        entity.MeasureUnitId = request.MeasureUnitId;
        entity.Quantity = request.Quantity;
        entity.QuantityType = request.QuantityType;
        entity.OutputType = request.OutputType;
        entity.PreferredWarehouseId = request.PreferredWarehouseId;
        entity.Notes = request.Notes;
        entity.SortIndex = request.SortIndex;

        await childRepository.SaveChangesAsync(cancellationToken);
    }
}
