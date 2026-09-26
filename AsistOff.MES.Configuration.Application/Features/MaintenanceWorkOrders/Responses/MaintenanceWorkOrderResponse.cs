using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Responses;

public record MaintenanceWorkOrderResponse(
    Guid Id,
    string Code,
    string Title,
    string? Description,
    Guid MachineId,
    string? MachineCode,
    Guid? PlanId,
    MaintenanceWorkOrderPriority Priority,
    MaintenanceWorkOrderStatus Status,
    DateTime ReportedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? ResolutionNotes);

public class PagedMaintenanceWorkOrdersResponse(
    IReadOnlyCollection<MaintenanceWorkOrderResponse> items,
    int totalCount,
    int? pageSize)
    : PagedResponse<MaintenanceWorkOrderResponse>(items, totalCount, pageSize);
