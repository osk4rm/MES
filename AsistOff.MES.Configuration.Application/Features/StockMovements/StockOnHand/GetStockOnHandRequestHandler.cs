using AsistOff.MES.Configuration.Application.Features.StockMovements.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.StockMovements.StockOnHand;

internal sealed class GetStockOnHandRequestHandler(
    IStockMovementsRepository stockMovementsRepository)
    : IRequestHandler<GetStockOnHandRequest, IReadOnlyList<StockOnHandResponse>>
{
    public async Task<IReadOnlyList<StockOnHandResponse>> Handle(
        GetStockOnHandRequest request, CancellationToken cancellationToken)
    {
        // Tenant isolation comes from the global EF query filter inside the
        // repository; no manual TenantId predicate is written here.
        var lines = await stockMovementsRepository.ListAsync(cancellationToken);

        return lines
            .Where(x => !request.ProductId.HasValue || x.ProductId == request.ProductId.Value)
            .Where(x => !request.WarehouseId.HasValue || x.WarehouseId == request.WarehouseId.Value)
            .GroupBy(x => new { x.ProductId, x.WarehouseId })
            .Select(g => new StockOnHandResponse(
                g.Key.ProductId,
                g.Key.WarehouseId,
                g.Sum(x => x.MovementType == StockMovement.ReceiptType ? x.Quantity : -x.Quantity)))
            .OrderBy(x => x.ProductId)
            .ThenBy(x => x.WarehouseId)
            .ToList();
    }
}
