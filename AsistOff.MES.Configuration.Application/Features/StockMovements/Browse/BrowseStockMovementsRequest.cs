using AsistOff.MES.Configuration.Application.Features.StockMovements.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.StockMovements.Browse;

/// <summary>
/// Minimal stock ledger read: returns exactly the lines posted for one
/// confirmation. The filter is required; unknown confirmation ids return 404.
/// </summary>
public class BrowseStockMovementsRequest : ITenantRequest<IReadOnlyList<StockMovementResponse>>
{
    public Guid? ConfirmationId { get; set; }
}
