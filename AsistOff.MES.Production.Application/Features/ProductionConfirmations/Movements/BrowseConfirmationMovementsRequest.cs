using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;

namespace AsistOff.MES.Production.Application.Features.ProductionConfirmations.Movements;

/// <summary>
/// Read-only RW/PW movement preview for a single confirmation: one PW line
/// for its good quantity plus RW lines scaled from the released recipe BOM.
/// </summary>
public record BrowseConfirmationMovementsRequest(Guid ConfirmationId)
    : ITenantRequest<IReadOnlyList<MovementPreviewLine>>;
