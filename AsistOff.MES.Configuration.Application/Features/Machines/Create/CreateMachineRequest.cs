using AsistOff.MES.Configuration.Application.Features.Machines.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.Machines.Create;

public record CreateMachineRequest(
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    Guid? DepartmentId,
    string? SyncId,
    decimal? Capacity = null,
    decimal? EfficiencyFactor = null) : ITenantRequest<MachineResponse>;
