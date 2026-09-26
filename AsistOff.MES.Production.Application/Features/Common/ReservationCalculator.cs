using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Exceptions;

namespace AsistOff.MES.Production.Application.Features.Common;

/// <summary>
/// One aggregated reservation requirement derived from the released recipe
/// BOM: the total quantity of a single (product, warehouse) pair the order
/// needs. A null <c>WarehouseId</c> is the unassigned bucket (BOM items
/// without a preferred warehouse).
/// </summary>
public sealed record ReservationRequirement(Guid ProductId, Guid? WarehouseId, decimal Quantity);

/// <summary>
/// Pure reserve/relieve math for soft material reservations (issue #291).
/// Kept static so it is trivially unit-testable without a database, next to
/// <see cref="MovementCalculator"/>.
/// </summary>
public static class ReservationCalculator
{
    /// <summary>
    /// Aggregates one requirement per distinct (ProductId, PreferredWarehouseId)
    /// from the released recipe BOM items, scaling each item with
    /// <see cref="MovementCalculator.ScaleBomQuantity"/> against the order
    /// planned quantity (scrap percentage included). Requirements with a
    /// non-positive quantity are rejected with <see cref="ValidationException"/>.
    /// </summary>
    public static IReadOnlyList<ReservationRequirement> BuildRequirements(
        ProductionOrder order, IReadOnlyCollection<BomItem> bomItems)
    {
        return bomItems
            .GroupBy(x => new { x.ProductId, x.PreferredWarehouseId })
            .Select(g =>
            {
                var quantity = g.Sum(item =>
                    MovementCalculator.ScaleBomQuantity(item, order.PlannedQuantity, 1));

                if (quantity <= 0)
                    throw new ValidationException(
                        nameof(BomItem.Quantity),
                        $"Reservation quantity for product {g.Key.ProductId} must be greater than zero.");

                return new ReservationRequirement(g.Key.ProductId, g.Key.PreferredWarehouseId, quantity);
            })
            .OrderBy(x => x.ProductId)
            .ThenBy(x => x.WarehouseId)
            .ToList();
    }

    /// <summary>
    /// Drops requirements that already have a reservation row for the order
    /// (matching product and warehouse, null warehouse equals null). This is
    /// the primary re-release idempotency guard; the unique database
    /// constraint is the backstop for warehouse-bound races.
    /// </summary>
    public static IReadOnlyList<ReservationRequirement> ExcludeAlreadyReserved(
        IReadOnlyList<ReservationRequirement> requirements,
        IReadOnlyCollection<MaterialReservation> existing)
    {
        return requirements
            .Where(r => !existing.Any(e =>
                e.ProductId == r.ProductId && e.WarehouseId == r.WarehouseId))
            .ToList();
    }

    /// <summary>
    /// Relieves open reservations FIFO from the RW movement lines of one
    /// confirmation (mutates the passed entities in place). Each RW line is
    /// consumed by the oldest matching open reservation first; a line that
    /// exceeds the remaining reserved quantity closes the reservation and
    /// the surplus is ignored (over-confirmation never drives a reservation
    /// negative). PW receipt lines are ignored.
    /// </summary>
    public static void ApplyRelief(
        IReadOnlyCollection<MaterialReservation> reservations,
        IReadOnlyCollection<MovementPreviewLine> lines)
    {
        foreach (var line in lines.Where(x => x.MovementType == MovementCalculator.IssueType))
        {
            var toRelieve = line.Quantity;
            if (toRelieve <= 0)
                continue;

            var matches = reservations
                .Where(r => r.Status != ReservationStatus.Closed
                    && r.ProductId == line.ProductId
                    && r.WarehouseId == line.PreferredWarehouseId
                    && r.RemainingQuantity > 0)
                .OrderBy(r => r.CreatedAt)
                .ThenBy(r => r.Id);

            foreach (var reservation in matches)
            {
                if (toRelieve <= 0)
                    break;

                var take = Math.Min(toRelieve, reservation.RemainingQuantity);
                if (take <= 0)
                    continue;

                reservation.QuantityRelieved += take;
                toRelieve -= take;
                reservation.Status = reservation.RemainingQuantity <= 0
                    ? ReservationStatus.Closed
                    : ReservationStatus.PartiallyRelieved;
            }
        }
    }
}
