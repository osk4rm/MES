using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.MaterialReservations;

/// <summary>
/// Single reservation read. Unknown ids — including rows of another tenant,
/// which the global query filter hides — surface as 404.
/// </summary>
public record GetMaterialReservationRequest(Guid Id) : ITenantRequest<MaterialReservationResponse>;
