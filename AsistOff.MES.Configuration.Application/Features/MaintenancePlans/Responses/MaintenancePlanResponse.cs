using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Responses;

public record MaintenancePlanResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    Guid MachineId,
    string? MachineCode,
    MaintenancePlanTriggerType TriggerType,
    int? IntervalDays,
    decimal? MeterIntervalValue,
    DateTime? NextDueAt,
    DateTime? LastCompletedAt,
    bool IsActive);

public class PagedMaintenancePlansResponse(
    IReadOnlyCollection<MaintenancePlanResponse> items,
    int totalCount,
    int? pageSize)
    : PagedResponse<MaintenancePlanResponse>(items, totalCount, pageSize);
