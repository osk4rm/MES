using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Movements;

internal sealed class BrowseOrderMovementsRequestHandler(
    IProductionOrdersRepository ordersRepository,
    IProductionConfirmationsRepository confirmationsRepository,
    IChildEntitiesRepository childEntitiesRepository)
    : IRequestHandler<BrowseOrderMovementsRequest, IReadOnlyList<MovementPreviewLine>>
{
    public async Task<IReadOnlyList<MovementPreviewLine>> Handle(
        BrowseOrderMovementsRequest request, CancellationToken cancellationToken)
    {
        var order = await ordersRepository.GetAsync(request.ProductionOrderId, cancellationToken)
            ?? throw new NotFoundException("ProductionOrder", request.ProductionOrderId);

        var confirmations = await confirmationsRepository.ListForOrderAsync(order.Id, cancellationToken);
        var producedQuantity = confirmations.Sum(x => x.GoodQuantity);

        var bomItems = await childEntitiesRepository.ListBomItemsForVersionAsync(
            order.RecipeVersionId, cancellationToken);

        return MovementCalculator.BuildPreview(order, producedQuantity, confirmations.Count, bomItems);
    }
}
