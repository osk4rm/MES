using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Release;

internal sealed class ReleaseProductionOrderRequestHandler(
    IProductionOrdersRepository ordersRepository,
    IRecipeVersionsRepository versionsRepository,
    IDateTimeProvider dateTimeProvider,
    ICurrentUserAccessor currentUserAccessor)
    : IRequestHandler<ReleaseProductionOrderRequest, ProductionOrderResponse>
{
    public async Task<ProductionOrderResponse> Handle(ReleaseProductionOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await ordersRepository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("ProductionOrder", request.Id);

        if (order.Status != ProductionOrderStatus.Planned)
            throw new ConflictException("Only orders in Planned status can be released.");

        var version = await versionsRepository.GetAsync(order.RecipeVersionId, cancellationToken);
        if (version is null || version.Status != RecipeVersionStatus.Released)
            throw new ValidationException(nameof(order.RecipeVersionId), "The referenced recipe version is not released.");

        order.Status = ProductionOrderStatus.Released;
        order.ReleasedAt = dateTimeProvider.UtcNow;
        order.ReleasedByUserId = currentUserAccessor.UserId;

        await ordersRepository.UpdateAsync(order, cancellationToken);

        return ProductionOrderMappers.Map(order);
    }
}
