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

/// <summary>Shape of an Andon signal as returned by <c>/api/andon-signals</c>.</summary>
public sealed record AndonSignalDto(
    Guid Id,
    Guid MachineId,
    short Category,
    Guid? ReasonCodeId,
    short Status,
    DateTime RaisedAt,
    DateTime? AcknowledgedAt,
    DateTime? ResolvedAt,
    string? Notes,
    Guid? RaisedByOperatorId,
    Guid? ProductionOrderId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Shape of the paged responses returned by browse endpoints.</summary>
public sealed record PagedResponseDto<T>(
    int TotalCount,
    int TotalPages,
    IReadOnlyCollection<T> Items);

/// <summary>Shape of a production order as returned by <c>/api/production-orders</c>.</summary>
public sealed record ProductionOrderDto(
    Guid Id,
    string Code,
    Guid ProductId,
    Guid RecipeId,
    Guid RecipeVersionId,
    decimal PlannedQuantity,
    Guid? MeasureUnitId,
    int Priority,
    DateTime? DueDate,
    short Status,
    DateTime? ReleasedAt,
    Guid? ReleasedByUserId,
    string? Notes,
    string? SyncId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Shape of a recipe as returned by <c>/api/recipes</c>.</summary>
public sealed record RecipeDto(
    Guid Id,
    string Code,
    string Name,
    IReadOnlyCollection<RecipeVersionDto> Versions);

/// <summary>Shape of a recipe version summary as returned by <c>/api/recipes</c>.</summary>
public sealed record RecipeVersionDto(
    Guid Id,
    int VersionNumber,
    short Status);
/// <summary>Shape of an SPC characteristic as returned by <c>/api/spc-characteristics</c>.</summary>
public sealed record SpcCharacteristicDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    Guid? ProductId,
    Guid? MachineId,
    short ChartType,
    decimal? NominalValue,
    decimal? LowerSpecLimit,
    decimal? UpperSpecLimit,
    decimal? LowerControlLimit,
    decimal? UpperControlLimit,
    int SampleSize,
    string? Unit,
    bool IsActive);

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

/// <summary>Shape of a shift as returned by <c>/api/shifts</c>. Times are ISO 8601 <c>HH:mm:ss</c>.</summary>
public sealed record ShiftDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string StartTime,
    string EndTime,
    bool IsActive);

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

/// <summary>Shape of a production confirmation as returned by <c>/api/production-confirmations</c>.</summary>
public sealed record ProductionConfirmationDto(
    Guid Id,
    Guid ProductionOrderId,
    Guid MachineId,
    Guid? ReportedByOperatorId,
    DateTime ReportedAt,
    decimal GoodQuantity,
    decimal ScrapQuantity,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Shape of a telemetry tag as returned by <c>/api/telemetry-tags</c>.</summary>
public sealed record MachineTelemetryTagDto(
    Guid Id,
    Guid MachineId,
    string NodeId,
    string DisplayName,
    short DataType,
    int PollIntervalSeconds,
    bool IsEnabled,
    string? Description,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Shape of a telemetry reading as returned by <c>/api/telemetry-readings</c>.</summary>
public sealed record TelemetryReadingDto(
    Guid Id,
    Guid TagId,
    Guid MachineId,
    DateTime ReadAt,
    double? DoubleValue,
    string? StringValue,
    short Quality);

/// <summary>Shape of one per-tag entry returned by <c>/api/telemetry-tags/status</c>.</summary>
public sealed record TelemetryTagStatusDto(
    Guid TagId,
    Guid MachineId,
    string NodeId,
    string DisplayName,
    bool IsEnabled,
    DateTime? LastReadAt,
    int ReadingsLastHour,
    bool Stale);

/// <summary>Shape of the connection-status readout returned by <c>/api/telemetry-tags/status</c>.</summary>
public sealed record TelemetryStatusDto(
    bool SimulatorEnabled,
    int SimulatorIntervalSeconds,
    IReadOnlyCollection<TelemetryTagStatusDto> Tags);
