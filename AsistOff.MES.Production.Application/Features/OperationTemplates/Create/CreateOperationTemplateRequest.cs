using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.OperationTemplates.Create;

public record CreateOperationTemplateRequest(
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
    decimal? QueueTimeMinutes) : ITenantRequest<OperationTemplateResponse>;
