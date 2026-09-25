using AsistOff.MES.Configuration.Application.Features.Machines.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Configuration.Application.Features.Machines.Create;

[RequirePermission(RbacDefaults.ConfigurationWrite)]
public record CreateMachineRequest(
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    Guid? DepartmentId,
    string? SyncId,
    decimal? Capacity = null,
    decimal? EfficiencyFactor = null) : ITenantRequest<MachineResponse>;
