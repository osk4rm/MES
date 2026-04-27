using AsistOff.MES.Configuration.Application.Features.Machines.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.Machines.Get;

public record GetMachineRequest(Guid Id) : ITenantRequest<MachineResponse>;
