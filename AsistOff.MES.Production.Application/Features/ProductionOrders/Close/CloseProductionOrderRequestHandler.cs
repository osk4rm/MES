using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Close;

internal sealed class CloseProductionOrderRequestHandler(
    IProductionOrdersRepository ordersRepository,
    IProductionConfirmationsRepository confirmationsRepository,
    IMaterialReservationsRepository reservationsRepository,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CloseProductionOrderRequest, ProductionOrderResponse>
{
    public async Task<ProductionOrderResponse> Handle(CloseProductionOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await ordersRepository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("ProductionOrder", request.Id);

        ProductionOrderConcurrency.RequireMatchIfPresent(order, request.ConcurrencyToken);

        if (order.Status != ProductionOrderStatus.Completed)
            throw new ConflictException("Only orders in Completed status can be closed.");

        var (produced, scrapped, count) = await confirmationsRepository.GetTotalsAsync(order.Id, cancellationToken);

        // Closing settles soft reservations (issue #291): whatever was never
        // relieved by RW confirmations is closed, so it stops reducing stock
        // availability.
        var reservations = await reservationsRepository.ListForOrderAsync(order.Id, cancellationToken);
        var now = dateTimeProvider.UtcNow;
        var open = reservations.Where(r => r.Status != ReservationStatus.Closed).ToList();

        // Snapshot the tracked mutations so a transaction failure below can
        // restore them: without this the scoped DefaultContext keeps the
        // rolled-back Closed mutations and a later SaveChanges could
        // persist them.
        var previousOrderStatus = order.Status;
        var previousOrderUpdatedAt = order.UpdatedAt;
        var reservationBefore = open.ToDictionary(
            r => r.Id, r => (Status: r.Status, UpdatedAt: r.UpdatedAt));

        foreach (var reservation in open)
        {
            reservation.Status = ReservationStatus.Closed;
            reservation.UpdatedAt = now;
        }

        order.Status = ProductionOrderStatus.Closed;
        order.UpdatedAt = now;

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await ordersRepository.UpdateAsync(order, cancellationToken);
                foreach (var reservation in open)
                    await reservationsRepository.UpdateAsync(reservation, cancellationToken);
            }, cancellationToken);
        }
        catch
        {
            foreach (var reservation in open)
            {
                var before = reservationBefore[reservation.Id];
                reservation.Status = before.Status;
                reservation.UpdatedAt = before.UpdatedAt;
            }

            order.Status = previousOrderStatus;
            order.UpdatedAt = previousOrderUpdatedAt;
            throw;
        }

        return ProductionOrderMappers.Map(order, produced, scrapped, count);
    }
}
