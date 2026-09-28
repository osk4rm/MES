using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Oee.Snapshot;

/// <summary>
/// Read-only per-Work Center OEE snapshot over a caller-supplied UTC window.
/// Computed at read time from the Work Center calendar (planned time),
/// closed downtime events (unplanned stops) and production confirmations
/// (good/scrap counts). No new tables; the ideal cycle time is optional —
/// when omitted it is resolved from the confirmed orders' operations
/// (routing master data, same rule as the summary endpoint). An explicit
/// positive value wins; a null ideal with no resolvable routing data is a
/// 400 naming <c>IdealCycleTimeSeconds</c>.
/// </summary>
public record GetOeeSnapshotRequest(
    Guid MachineId,
    DateTime FromUtc,
    DateTime ToUtc,
    decimal? IdealCycleTimeSeconds) : ITenantRequest<OeeSnapshotResponse>;
