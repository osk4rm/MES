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
    DateTime? UpdatedAt,
    decimal ProducedQuantity,
    decimal ScrappedQuantity,
    decimal RemainingQuantity,
    int ConfirmationsCount,
    DateTime? CompletedAt,
    DateTime? ClosedAt);

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

/// <summary>Shape of a kanban loop as returned by <c>/api/kanban/loops</c>.</summary>
public sealed record KanbanLoopDto(
    Guid Id,
    string Code,
    Guid ProductId,
    Guid ConsumingMachineId,
    Guid SupplyingWarehouseId,
    decimal CardQuantity,
    int CardsInCirculation,
    bool IsActive,
    string? Notes);

/// <summary>Shape of a kanban card as returned by <c>/api/kanban/loops/{loopId}/cards</c>.</summary>
public sealed record KanbanCardDto(
    Guid Id,
    Guid LoopId,
    string CardNumber,
    short Status,
    string? Notes);

/// <summary>Shape of a lot genealogy edge as returned by <c>/api/lot-genealogy</c>.</summary>
public sealed record LotGenealogyEdgeDto(
    Guid Id,
    Guid ConsumedLotId,
    Guid ProducedLotId,
    Guid ProductionOrderId,
    Guid? ProductionConfirmationId,
    Guid MachineId,
    Guid? ReportedByOperatorId,
    decimal ConsumedQuantity,
    DateTime OccurredAt,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Shape of one node returned by the lot traceability endpoints.</summary>
public sealed record LotTraceabilityNodeDto(
    Guid LotId,
    string LotCode,
    Guid ProductId,
    int Depth,
    decimal ConsumedQuantity,
    Guid ProductionOrderId,
    string ProductionOrderCode,
    Guid MachineId,
    Guid? ReportedByOperatorId,
    DateTime OccurredAt);

/// <summary>Shape of the transitive closure returned by the lot traceability endpoints.</summary>
public sealed record LotTraceabilityDto(
    Guid RootLotId,
    string RootLotCode,
    IReadOnlyCollection<LotTraceabilityNodeDto> Nodes,
    bool Truncated);

/// <summary>Shape of one RW/PW movement preview line returned by the movements endpoints.</summary>
public sealed record MovementPreviewLineDto(
    string MovementType,
    Guid ProductId,
    decimal Quantity,
    Guid? MeasureUnitId,
    Guid? PreferredWarehouseId);

/// <summary>Shape of the OEE snapshot returned by <c>/api/oee/snapshot</c>.</summary>
public sealed record OeeSnapshotDto(
    Guid MachineId,
    DateTime FromUtc,
    DateTime ToUtc,
    decimal IdealCycleTimeSeconds,
    double? Availability,
    double? Performance,
    double? Quality,
    double? Oee,
    bool AvailabilityComputed,
    bool PerformanceComputed,
    bool QualityComputed,
    double PlannedProductionTimeMinutes,
    double RunTimeMinutes,
    double DowntimeMinutes,
    decimal TotalCount,
    decimal GoodCount,
    decimal ScrapCount);

/// <summary>Shape of the OEE summary returned by <c>/api/oee</c>: counts, Quality, Availability, auto-resolved ideal cycle time, Performance (clamped at 1) and composite OEE.</summary>
public sealed record OeeSummaryDto(
    Guid MachineId,
    DateTime FromUtc,
    DateTime ToUtc,
    decimal GoodCount,
    decimal ScrapCount,
    decimal TotalCount,
    double? Quality,
    double PlannedTimeMinutes,
    double RunTimeMinutes,
    double DowntimeMinutes,
    double? Availability,
    decimal? IdealCycleTimeSeconds,
    double? Performance,
    double? Oee);

/// <summary>Shape of the OEE trend returned by <c>/api/oee/trend</c>.</summary>
public sealed record OeeTrendDto(
    Guid MachineId,
    DateTime FromUtc,
    DateTime ToUtc,
    decimal IdealCycleTimeSeconds,
    string Bucket,
    IReadOnlyCollection<OeeSnapshotDto> Buckets);

/// <summary>Shape of one downtime Pareto row returned by <c>/api/oee/losses</c>.</summary>
public sealed record DowntimeParetoEntryDto(
    Guid ReasonCodeId,
    string? Code,
    string? DisplayName,
    double Minutes,
    double Share);

/// <summary>Shape of one scrap Pareto row returned by <c>/api/oee/losses</c>.</summary>
public sealed record ScrapParetoEntryDto(
    Guid ReasonCodeId,
    string? Code,
    string? DisplayName,
    decimal Quantity,
    double Share);

/// <summary>Shape of the loss Pareto returned by <c>/api/oee/losses</c>.</summary>
public sealed record OeeLossesDto(
    Guid MachineId,
    DateTime FromUtc,
    DateTime ToUtc,
    double TotalDowntimeMinutes,
    IReadOnlyCollection<DowntimeParetoEntryDto> DowntimePareto,
    decimal TotalScrapQuantity,
    IReadOnlyCollection<ScrapParetoEntryDto> ScrapPareto);

/// <summary>Shape of the reliability snapshot returned by <c>/api/reliability/snapshot</c>.</summary>
public sealed record ReliabilitySnapshotDto(
    Guid MachineId,
    DateTime FromUtc,
    DateTime ToUtc,
    int FailureCount,
    int RepairCount,
    double WindowMinutes,
    double UptimeMinutes,
    double TotalDowntimeMinutes,
    double? MtbfMinutes,
    double? MttrMinutes,
    double? AvgRepairMinutes);

/// <summary>Shape of an OPC UA connection as returned by <c>/api/opcua-connections</c>.</summary>
public sealed record OpcUaConnectionDto(
    Guid Id,
    Guid MachineId,
    string EndpointUrl,
    short SecurityPolicy,
    int PollIntervalSeconds,
    bool IsEnabled,
    DateTime? LastSeenAtUtc,
    string? LastError,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Shape of the endpoint check returned by <c>/api/opcua-connections/{id}/test</c>.</summary>
public sealed record OpcUaConnectionTestDto(
    Guid Id,
    string EndpointUrl,
    bool Reachable,
    DateTime CheckedAt);

/// <summary>Shape of one per-connection entry returned by <c>/api/opcua-connections/status</c>.</summary>
public sealed record OpcUaConnectionStatusEntryDto(
    Guid ConnectionId,
    Guid MachineId,
    string EndpointUrl,
    bool IsEnabled,
    DateTime? LastSeenAtUtc,
    string? LastError,
    bool IsLive,
    int TotalTags,
    int ReportingTags,
    int StaleTags);

/// <summary>Shape of an operator shift assignment as returned by <c>/api/operator-shift-assignments</c>. Date is ISO 8601 <c>yyyy-MM-dd</c>.</summary>
public sealed record OperatorShiftAssignmentDto(
    Guid Id,
    Guid OperatorId,
    string? OperatorIdentifier,
    string? OperatorName,
    Guid ShiftId,
    string? ShiftCode,
    string? ShiftName,
    string Date,
    string? Notes);

/// <summary>Shape of the connection-status readout returned by <c>/api/opcua-connections/status</c>.</summary>
public sealed record OpcUaConnectionStatusDto(
    IReadOnlyCollection<OpcUaConnectionStatusEntryDto> Connections,
    int TotalCount,
    int LiveCount,
    int StaleCount,
    int DisabledCount);
