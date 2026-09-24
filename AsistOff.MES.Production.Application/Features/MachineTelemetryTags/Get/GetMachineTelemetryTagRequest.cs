using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Get;

public record GetMachineTelemetryTagRequest(Guid Id) : ITenantRequest<MachineTelemetryTagResponse>;
