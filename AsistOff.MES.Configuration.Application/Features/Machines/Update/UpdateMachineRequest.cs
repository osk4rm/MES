using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.Machines.Update;

public record UpdateMachineRequest(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    Guid? DepartmentId,
    string? SyncId,
    decimal? Capacity = null,
    decimal? EfficiencyFactor = null) : ITenantRequest;
