using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.OperationTemplates.Update;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record UpdateOperationTemplateRequest(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string? OperationType,
    bool IsActive,
    decimal? SetupTimeMinutes,
    RunTimeMode RunTimeMode,
    decimal? RunTimePerUnitSeconds,
    decimal? RunTimePerBatchMinutes,
    decimal? TeardownTimeMinutes,
    decimal? QueueTimeMinutes) : ITenantRequest;
