using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.ShiftHandovers.Create;

/// <summary>
/// Persists one shift handover logbook entry for a Work Center over a
/// caller-supplied UTC window (max 24h). Append-only: corrections are new
/// entries, never updates.
/// </summary>
[RequirePermission(RbacDefaults.ProductionWrite)]
public record CreateShiftHandoverRequest(
    Guid MachineId,
    DateTime From,
    DateTime To,
    string Notes) : ITenantRequest<ShiftHandoverResponse>;
