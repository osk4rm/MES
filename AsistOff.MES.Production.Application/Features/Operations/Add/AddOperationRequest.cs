using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.Operations.Add;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record AddOperationRequest(
    Guid VersionId,
    string Code,
    string Name,
    string? Description,
    string? OperationType,
    int? SortIndex,
    decimal? SetupTimeMinutes,
    RunTimeMode RunTimeMode,
    decimal? RunTimePerUnitSeconds,
    decimal? RunTimePerBatchMinutes,
    decimal? TeardownTimeMinutes,
    decimal? QueueTimeMinutes,
    bool IsOptional,
    bool AllowParallelExecution,
    decimal? ExpectedQuantity) : ITenantRequest<OperationNodeResponse>;
