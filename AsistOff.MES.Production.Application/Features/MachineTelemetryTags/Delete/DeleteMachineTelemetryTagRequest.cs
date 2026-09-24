using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Delete;

public record DeleteMachineTelemetryTagRequest(Guid Id) : ITenantRequest;
