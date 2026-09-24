using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Toggle;

public record ToggleMachineTelemetryTagRequest(Guid Id) : ITenantRequest<MachineTelemetryTagResponse>;
