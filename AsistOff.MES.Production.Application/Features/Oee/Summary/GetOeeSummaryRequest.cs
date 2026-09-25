using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Oee.Summary;

/// <summary>
/// Read-only per-Work Center OEE summary over a caller-supplied UTC window.
/// First slice (issue #180): the Quality factor plus raw confirmation counts,
/// computed at read time from <c>ProductionConfirmation</c> aggregates. Later
/// slices extend this contract with Availability and Performance.
/// No new tables and no migration.
/// </summary>
public record GetOeeSummaryRequest(
    Guid MachineId,
    DateTime FromUtc,
    DateTime ToUtc) : ITenantRequest<OeeSummaryResponse>;
