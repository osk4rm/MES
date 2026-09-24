using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Movements;

/// <summary>
/// Read-only RW/PW movement preview aggregated per order: one PW line with
/// the summed good quantity of the caller tenant confirmations plus RW lines
/// scaled from the released recipe BOM.
/// </summary>
public record BrowseOrderMovementsRequest(Guid ProductionOrderId)
    : ITenantRequest<IReadOnlyList<MovementPreviewLine>>;
