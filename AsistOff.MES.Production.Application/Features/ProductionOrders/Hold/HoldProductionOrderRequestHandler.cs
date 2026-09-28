using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Hold;

internal sealed class HoldProductionOrderRequestHandler(
    IProductionOrdersRepository ordersRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<HoldProductionOrderRequest, ProductionOrderResponse>
{
    internal const int MaxHoldReasonLength = 500;

    public async Task<ProductionOrderResponse> Handle(HoldProductionOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await ordersRepository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("ProductionOrder", request.Id);

        ProductionOrderConcurrency.RequireMatchIfPresent(order, request.ConcurrencyToken);

        if (order.Status == ProductionOrderStatus.OnHold)
            throw new ConflictException($"Production order '{order.Code}' is already on hold.");

        if (order.Status is not (ProductionOrderStatus.Released or ProductionOrderStatus.InProgress))
            throw new ConflictException("Only orders in Released or InProgress status can be put on hold.");

        if (request.HoldReason is { Length: > MaxHoldReasonLength })
            throw new ValidationException(nameof(request.HoldReason), $"Hold reason cannot exceed {MaxHoldReasonLength} characters.");

        order.StatusBeforeHold = order.Status;
        order.Status = ProductionOrderStatus.OnHold;
        order.HeldAtUtc = dateTimeProvider.UtcNow;
        order.HoldReason = string.IsNullOrWhiteSpace(request.HoldReason) ? null : request.HoldReason;
        order.UpdatedAt = dateTimeProvider.UtcNow;

        await ordersRepository.UpdateAsync(order, cancellationToken);

        return ProductionOrderMappers.Map(order);
    }
}
