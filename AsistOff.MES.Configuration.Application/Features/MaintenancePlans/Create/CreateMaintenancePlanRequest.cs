using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Responses;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Create;

[RequirePermission(RbacDefaults.ConfigurationWrite)]
public record CreateMaintenancePlanRequest(
    string Code,
    string Name,
    string? Description,
    Guid MachineId,
    MaintenancePlanTriggerType TriggerType,
    int? IntervalDays,
    decimal? MeterIntervalValue,
    DateTime? NextDueAt,
    bool IsActive = true) : ITenantRequest<MaintenancePlanResponse>;
