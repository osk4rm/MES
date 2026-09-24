namespace AsistOff.MES.Integration.Tests.TestData;

/// <summary>Shape of a reason code as returned by <c>/api/reason-codes</c>.</summary>
public sealed record ReasonCodeDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    short Category,
    bool IsActive,
    int SortIndex);

/// <summary>Shape of the paged responses returned by browse endpoints.</summary>
public sealed record PagedResponseDto<T>(
    int TotalCount,
    int TotalPages,
    IReadOnlyCollection<T> Items);

/// <summary>Shape of a machine as returned by <c>/api/machines</c>.</summary>
public sealed record MachineDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    Guid? DepartmentId,
    string? DepartmentCode,
    string? DepartmentName,
    string? SyncId);

/// <summary>Shape of a maintenance work order as returned by <c>/api/maintenance-work-orders</c>.</summary>
public sealed record MaintenanceWorkOrderDto(
    Guid Id,
    string Code,
    string Title,
    string? Description,
    Guid MachineId,
    string? MachineCode,
    short Priority,
    short Status,
    DateTime ReportedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? ResolutionNotes);

/// <summary>Shape of a scrap event as returned by <c>/api/scrap-events</c>.</summary>
public sealed record ScrapEventDto(
    Guid Id,
    Guid MachineId,
    Guid ReasonCodeId,
    decimal Quantity,
    DateTime ReportedAt,
    string? Notes,
    Guid? ReportedByOperatorId,
    Guid? ProductionOrderId);
