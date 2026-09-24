using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ProductionConfirmations.Movements;

internal sealed class BrowseConfirmationMovementsRequestHandler(
    IProductionConfirmationsRepository confirmationsRepository,
    IProductionOrdersRepository ordersRepository,
    IChildEntitiesRepository childEntitiesRepository)
    : IRequestHandler<BrowseConfirmationMovementsRequest, IReadOnlyList<MovementPreviewLine>>
{
    public async Task<IReadOnlyList<MovementPreviewLine>> Handle(
        BrowseConfirmationMovementsRequest request, CancellationToken cancellationToken)
    {
        var confirmation = await confirmationsRepository.GetAsync(request.ConfirmationId, cancellationToken)
            ?? throw new NotFoundException("ProductionConfirmation", request.ConfirmationId);

        var order = await ordersRepository.GetAsync(confirmation.ProductionOrderId, cancellationToken)
            ?? throw new NotFoundException("ProductionOrder", confirmation.ProductionOrderId);

        var bomItems = await childEntitiesRepository.ListBomItemsForVersionAsync(
            order.RecipeVersionId, cancellationToken);

        return MovementCalculator.BuildPreview(order, confirmation.GoodQuantity, 1, bomItems);
    }
}
