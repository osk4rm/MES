using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;

namespace AsistOff.MES.Configuration.Application.Features.MaterialReservations;

/// <summary>
/// One soft material reservation row (issue #291).
/// </summary>
public sealed record MaterialReservationResponse(
    Guid Id,
    Guid ProductionOrderId,
    Guid ProductId,
    Guid? WarehouseId,
    decimal QuantityReserved,
    decimal QuantityRelieved,
    decimal RemainingQuantity,
    ReservationStatus Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    internal static MaterialReservationResponse Map(MaterialReservation e) => new(
        e.Id,
        e.ProductionOrderId,
        e.ProductId,
        e.WarehouseId,
        e.QuantityReserved,
        e.QuantityRelieved,
        e.RemainingQuantity,
        e.Status,
        e.CreatedAt,
        e.UpdatedAt);
}
