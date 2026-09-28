using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Resume;

internal sealed class ResumeProductionOrderRequestHandler(
    IProductionOrdersRepository ordersRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ResumeProductionOrderRequest, ProductionOrderResponse>
{
    public async Task<ProductionOrderResponse> Handle(ResumeProductionOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await ordersRepository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("ProductionOrder", request.Id);

        ProductionOrderConcurrency.RequireMatchIfPresent(order, request.ConcurrencyToken);

        if (order.Status != ProductionOrderStatus.OnHold)
            throw new ConflictException("Only orders on hold can be resumed.");

        // Restore the pre-hold status; a missing or unexpected stored value
        // falls back to Released so the order never lands in a dead status.
        order.Status = order.StatusBeforeHold is ProductionOrderStatus.Released or ProductionOrderStatus.InProgress
            ? order.StatusBeforeHold.Value
            : ProductionOrderStatus.Released;
        order.StatusBeforeHold = null;
        order.HeldAtUtc = null;
        order.HoldReason = null;
        order.UpdatedAt = dateTimeProvider.UtcNow;

        await ordersRepository.UpdateAsync(order, cancellationToken);

        return ProductionOrderMappers.Map(order);
    }
}
