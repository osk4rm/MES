using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.Lots;

internal static class LotMappings
{
    internal static LotResponse Map(Lot lot) => new(
        lot.Id,
        lot.Code,
        lot.ProductId,
        lot.MeasureUnitId,
        lot.Quantity,
        lot.Status,
        lot.SupplierLotNumber,
        lot.ProducedAt,
        lot.ExpiryDate,
        lot.Notes,
        lot.CreatedAt,
        lot.UpdatedAt);

    /// <summary>
    /// Allowed transitions: Available &lt;-&gt; OnHold, Available/OnHold -&gt;
    /// Consumed, Scrapped or Expired. Consumed, Scrapped and Expired are terminal.
    /// </summary>
    internal static bool IsTransitionAllowed(LotStatus from, LotStatus to)
    {
        if (from == to)
            return true;

        return (from, to) switch
        {
            (LotStatus.Available, LotStatus.OnHold) => true,
            (LotStatus.OnHold, LotStatus.Available) => true,
            (LotStatus.Available, LotStatus.Consumed) => true,
            (LotStatus.OnHold, LotStatus.Consumed) => true,
            (LotStatus.Available, LotStatus.Scrapped) => true,
            (LotStatus.OnHold, LotStatus.Scrapped) => true,
            (LotStatus.Available, LotStatus.Expired) => true,
            (LotStatus.OnHold, LotStatus.Expired) => true,
            _ => false
        };
    }
}
