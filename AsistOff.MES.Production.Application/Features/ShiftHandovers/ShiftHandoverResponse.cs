namespace AsistOff.MES.Production.Application.Features.ShiftHandovers;

/// <summary>
/// A persisted shift handover logbook entry (issue #293, slice 2/2).
/// <c>ShiftId</c> is null and <c>UncoveredShift</c> is true when the window
/// had no Work Center calendar entry. <c>OpenOrdersCount</c> and
/// <c>ActiveAndonCount</c> are denormalized snapshot counts taken at creation
/// time. Audit fields are server-set and immutable; history is append-only.
/// </summary>
public sealed record ShiftHandoverResponse(
    Guid Id,
    Guid MachineId,
    Guid? ShiftId,
    DateTime From,
    DateTime To,
    string Notes,
    Guid? CreatedByUserId,
    DateTime CreatedAt,
    int OpenOrdersCount,
    int ActiveAndonCount,
    bool UncoveredShift);
