using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.Operations.Add;

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
