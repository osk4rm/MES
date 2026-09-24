using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Complete;

internal sealed class CompleteProductionOrderRequestHandler(
    IProductionOrdersRepository ordersRepository,
    IProductionConfirmationsRepository confirmationsRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CompleteProductionOrderRequest, ProductionOrderResponse>
{
    public async Task<ProductionOrderResponse> Handle(CompleteProductionOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await ordersRepository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("ProductionOrder", request.Id);

        if (order.Status != ProductionOrderStatus.InProgress)
            throw new ConflictException("Only orders in InProgress status can be completed.");

        var (produced, scrapped, count) = await confirmationsRepository.GetTotalsAsync(order.Id, cancellationToken);
        if (produced <= 0)
            throw new ConflictException("Cannot complete an order without any good quantity confirmed.");

        order.Status = ProductionOrderStatus.Completed;
        order.UpdatedAt = dateTimeProvider.UtcNow;

        await ordersRepository.UpdateAsync(order, cancellationToken);

        return ProductionOrderMappers.Map(order, produced, scrapped, count);
    }
}
