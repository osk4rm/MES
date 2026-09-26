using AsistOff.MES.Configuration.Application.Features.StockMovements.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.StockMovements.StockOnHand;

internal sealed class GetStockOnHandRequestHandler(
    IStockMovementsRepository stockMovementsRepository,
    IMaterialReservationsRepository reservationsRepository)
    : IRequestHandler<GetStockOnHandRequest, IReadOnlyList<StockOnHandResponse>>
{
    public async Task<IReadOnlyList<StockOnHandResponse>> Handle(
        GetStockOnHandRequest request, CancellationToken cancellationToken)
    {
        // Tenant isolation comes from the global EF query filter inside the
        // repositories; no manual TenantId predicate is written here.
        var lines = await stockMovementsRepository.ListAsync(cancellationToken);

        // Open (non-Closed) reservation remainder per pair, for the
        // Available = OnHand - Reserved view (issue #291).
        var reservedByPair = (await reservationsRepository.ListOpenAsync(cancellationToken))
            .GroupBy(x => (x.ProductId, x.WarehouseId))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.RemainingQuantity));

        return lines
            .Where(x => !request.ProductId.HasValue || x.ProductId == request.ProductId.Value)
            .Where(x => !request.WarehouseId.HasValue || x.WarehouseId == request.WarehouseId.Value)
            .GroupBy(x => (x.ProductId, x.WarehouseId))
            .Select(g =>
            {
                var onHand = g.Sum(x => x.MovementType == StockMovement.ReceiptType ? x.Quantity : -x.Quantity);
                reservedByPair.TryGetValue(g.Key, out var reserved);
                return new StockOnHandResponse(
                    g.Key.ProductId,
                    g.Key.WarehouseId,
                    onHand,
                    reserved,
                    onHand - reserved);
            })
            .OrderBy(x => x.ProductId)
            .ThenBy(x => x.WarehouseId)
            .ToList();
    }
}
