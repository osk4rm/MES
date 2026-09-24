using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Oee.Snapshot;

/// <summary>
/// Read-only per-Work Center OEE snapshot over a caller-supplied UTC window.
/// Computed at read time from the Work Center calendar (planned time),
/// closed downtime events (unplanned stops) and production confirmations
/// (good/scrap counts). No new tables; ideal cycle time is a query parameter
/// because <c>Machine</c> carries no such field yet.
/// </summary>
public record GetOeeSnapshotRequest(
    Guid MachineId,
    DateTime FromUtc,
    DateTime ToUtc,
    decimal IdealCycleTimeSeconds) : ITenantRequest<OeeSnapshotResponse>;
