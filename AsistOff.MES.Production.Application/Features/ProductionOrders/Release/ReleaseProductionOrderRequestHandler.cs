using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Users.Core.Rbac;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Release;

internal sealed class ReleaseProductionOrderRequestHandler(
    IProductionOrdersRepository ordersRepository,
    IRecipeVersionsRepository versionsRepository,
    IChildEntitiesRepository childEntitiesRepository,
    IMaterialReservationsRepository reservationsRepository,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ICurrentUserAccessor currentUserAccessor,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ReleaseProductionOrderRequest, ProductionOrderResponse>
{
    public async Task<ProductionOrderResponse> Handle(ReleaseProductionOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await ordersRepository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("ProductionOrder", request.Id);

        ProductionOrderConcurrency.RequireMatchIfPresent(order, request.ConcurrencyToken);

        if (order.Status != ProductionOrderStatus.Planned)
            throw new ConflictException("Only orders in Planned status can be released.");

        var version = await versionsRepository.GetAsync(order.RecipeVersionId, cancellationToken);
        if (version is null || version.Status != RecipeVersionStatus.Released)
            throw new ValidationException(nameof(order.RecipeVersionId), "The referenced recipe version is not released.");

        // Soft material allocation (issue #291): one reservation row per
        // distinct (product, warehouse) from the released recipe BOM, scaled
        // by the order quantity. Orders without BOM items reserve nothing.
        // Re-release skips already-reserved pairs (the status guard above
        // already rejects it with 409; the skip plus the unique constraint
        // backstop concurrent double-release races).
        var bomItems = await childEntitiesRepository.ListBomItemsForVersionAsync(
            order.RecipeVersionId, cancellationToken);
        var requirements = ReservationCalculator.BuildRequirements(order, bomItems);
        var existing = await reservationsRepository.ListForOrderAsync(order.Id, cancellationToken);
        var missing = ReservationCalculator.ExcludeAlreadyReserved(requirements, existing);

        var now = dateTimeProvider.UtcNow;
        var reservations = missing.Select(r => new MaterialReservation
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            ProductionOrderId = order.Id,
            ProductId = r.ProductId,
            WarehouseId = r.WarehouseId,
            QuantityReserved = r.Quantity,
            QuantityRelieved = 0m,
            Status = Configuration.Domain.Enums.ReservationStatus.Active,
            CreatedAt = now
        }).ToList();

        order.Status = ProductionOrderStatus.Released;
        order.ReleasedAt = now;
        order.ReleasedByUserId = currentUserAccessor.UserId;

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await ordersRepository.UpdateAsync(order, cancellationToken);
            await reservationsRepository.AddRangeAsync(reservations, cancellationToken);
        }, cancellationToken);

        return ProductionOrderMappers.Map(order);
    }
}
