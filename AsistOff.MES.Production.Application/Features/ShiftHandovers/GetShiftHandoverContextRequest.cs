using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.ShiftHandovers;

/// <summary>
/// Read-only shift handover context over a caller-supplied UTC window
/// (max 24h). <c>MachineId</c> is optional: when given, the shift window is
/// resolved from that Work Center's calendar and signals/confirmations are
/// scoped to it; when omitted, the context is tenant-wide with no single
/// shift. Computed from existing tables only (no migration).
/// </summary>
public record GetShiftHandoverContextRequest(
    Guid? MachineId,
    DateTime From,
    DateTime To,
    int? ConfirmationPage = null,
    int? ConfirmationPageSize = null) : ITenantRequest<ShiftHandoverContextResponse>;
