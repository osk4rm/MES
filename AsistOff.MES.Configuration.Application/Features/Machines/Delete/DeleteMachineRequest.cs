using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.Machines.Delete;

public record DeleteMachineRequest(Guid Id) : ITenantRequest;
