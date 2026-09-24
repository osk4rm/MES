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

/// <summary>Shape of a downtime event as returned by <c>/api/downtime-events</c>.</summary>
public sealed record DowntimeEventDto(
    Guid Id,
    Guid MachineId,
    Guid ReasonCodeId,
    DateTime StartedAt,
    DateTime? EndedAt,
    short Status,
    double? DurationMinutes,
    string? Notes,
    Guid? ReportedByOperatorId,
    Guid? ProductionOrderId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Shape of a machine as returned by <c>/api/machines</c>.</summary>
public sealed record MachineDto(
    Guid Id,
    string Code,
    string Name);
/// <summary>Shape of a machine as returned by <c>/api/machines</c>.</summary>
/// <summary>Shape of a lot as returned by <c>/api/lots</c>.</summary>
public sealed record LotDto(
    Guid Id,
    string Code,
    Guid ProductId,
    Guid MeasureUnitId,
    decimal Quantity,
    short Status,
    string? SupplierLotNumber,
    DateTime? ProducedAt,
    DateTime? ExpiryDate,
    string? Notes);

/// <summary>Shape of a shift as returned by <c>/api/shifts</c>. Times are ISO 8601 <c>HH:mm:ss</c>.</summary>
public sealed record ShiftDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string StartTime,
    string EndTime,
    bool IsActive);

/// <summary>One weekly window returned by <c>/api/machines/{id}/calendar</c>.</summary>
public sealed record WorkCenterCalendarEntryDto(
    Guid Id,
    int DayOfWeek,
    string StartTime,
    string EndTime,
    Guid? ShiftId,
    bool IsWorking);

/// <summary>Shape of a Work Center calendar as returned by <c>/api/machines/{id}/calendar</c>.</summary>
public sealed record WorkCenterCalendarDto(
    Guid Id,
    Guid MachineId,
    string MachineCode,
    string MachineName,
    IReadOnlyCollection<WorkCenterCalendarEntryDto> Entries);

/// <summary>Shape of a Work Center as returned by <c>/api/machines</c>.</summary>
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
    bool IsActive);

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
