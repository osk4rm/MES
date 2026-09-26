using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.ShiftHandovers;

/// <summary>
/// Read-only shift handover context: the resolved shift window plus the open
/// Production Orders, active Andon signals and recent operator Confirmations
/// the incoming crew needs. Composed from existing tables only; no new
/// entities and no migration (issue #292, slice 1/2).
/// </summary>
public sealed record ShiftHandoverContextResponse(
    Guid? MachineId,
    Guid? ShiftId,
    DateTime From,
    DateTime To,
    bool UncoveredShift,
    IReadOnlyList<ShiftHandoverOrderItem> OpenOrders,
    IReadOnlyList<ShiftHandoverSignalItem> ActiveSignals,
    IReadOnlyList<ShiftHandoverConfirmationItem> Confirmations,
    int ConfirmationsTotalCount,
    int ConfirmationPage,
    int ConfirmationPageSize);

/// <summary>
/// An open (Released or InProgress) Production Order with read-time
/// confirmation totals. <c>ProductCode</c> is resolved read-time and is null
/// when the product row is absent.
/// </summary>
public sealed record ShiftHandoverOrderItem(
    Guid Id,
    string Code,
    Guid ProductId,
    string? ProductCode,
    decimal PlannedQuantity,
    decimal ProducedQuantity,
    decimal ScrappedQuantity,
    int Priority,
    DateTime? DueDate,
    ProductionOrderStatus Status);

/// <summary>
/// An active Andon signal. The signal table carries no code/severity columns:
/// <c>Severity</c> is the category name and <c>Code</c> is the linked
/// ReasonCode code, falling back to the category name when no ReasonCode is set.
/// </summary>
public sealed record ShiftHandoverSignalItem(
    Guid Id,
    Guid MachineId,
    AndonSignalCategory Category,
    string Severity,
    string Code,
    DateTime RaisedAt);

/// <summary>
/// A recent operator Confirmation, newest first. <c>OperatorCode</c> is the
/// reporting operator's identifier and is null for anonymous reports.
/// </summary>
public sealed record ShiftHandoverConfirmationItem(
    Guid Id,
    Guid ProductionOrderId,
    Guid MachineId,
    DateTime ReportedAt,
    decimal GoodQuantity,
    decimal ScrapQuantity,
    string? OperatorCode,
    string? Notes);
