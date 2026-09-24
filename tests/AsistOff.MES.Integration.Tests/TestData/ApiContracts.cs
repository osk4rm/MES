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
