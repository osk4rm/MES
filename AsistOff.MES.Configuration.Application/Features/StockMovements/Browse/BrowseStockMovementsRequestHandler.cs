using AsistOff.MES.Configuration.Application.Features.StockMovements.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.StockMovements.Browse;

internal sealed class BrowseStockMovementsRequestHandler(
    IStockMovementsRepository stockMovementsRepository,
    IProductionConfirmationsRepository confirmationsRepository)
    : IRequestHandler<BrowseStockMovementsRequest, IReadOnlyList<StockMovementResponse>>
{
    public async Task<IReadOnlyList<StockMovementResponse>> Handle(
        BrowseStockMovementsRequest request, CancellationToken cancellationToken)
    {
        if (!request.ConfirmationId.HasValue || request.ConfirmationId.Value == Guid.Empty)
            throw new ValidationException(nameof(request.ConfirmationId), "Confirmation id is required.");

        var confirmation = await confirmationsRepository.GetAsync(request.ConfirmationId.Value, cancellationToken)
            ?? throw new NotFoundException("ProductionConfirmation", request.ConfirmationId.Value);

        var lines = await stockMovementsRepository.ListForConfirmationAsync(confirmation.Id, cancellationToken);

        return lines.Select(Map).ToList();
    }

    internal static StockMovementResponse Map(StockMovement e) => new(
        e.Id, e.MovementType, e.ProductId, e.Quantity, e.MeasureUnitId, e.WarehouseId,
        e.ProductionConfirmationId, e.ProductionOrderId, e.ReportedAt);
}
