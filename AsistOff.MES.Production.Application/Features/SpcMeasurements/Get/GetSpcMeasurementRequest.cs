using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.SpcMeasurements.Get;

public record GetSpcMeasurementRequest(Guid Id) : ITenantRequest<SpcMeasurementResponse>;
