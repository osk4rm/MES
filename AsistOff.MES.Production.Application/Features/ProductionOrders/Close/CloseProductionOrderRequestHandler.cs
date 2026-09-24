using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Close;

internal sealed class CloseProductionOrderRequestHandler(
    IProductionOrdersRepository ordersRepository,
    IProductionConfirmationsRepository confirmationsRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CloseProductionOrderRequest, ProductionOrderResponse>
{
    public async Task<ProductionOrderResponse> Handle(CloseProductionOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await ordersRepository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("ProductionOrder", request.Id);

        if (order.Status != ProductionOrderStatus.Completed)
            throw new ConflictException("Only orders in Completed status can be closed.");

        var (produced, scrapped, count) = await confirmationsRepository.GetTotalsAsync(order.Id, cancellationToken);

        order.Status = ProductionOrderStatus.Closed;
        order.UpdatedAt = dateTimeProvider.UtcNow;

        await ordersRepository.UpdateAsync(order, cancellationToken);

        return ProductionOrderMappers.Map(order, produced, scrapped, count);
    }
}
