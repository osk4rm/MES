using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.TelemetryReadings.Get;

public record GetTelemetryReadingRequest(Guid Id) : ITenantRequest<TelemetryReadingResponse>;
