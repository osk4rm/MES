using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Oee.Summary;

/// <summary>
/// Read-only per-Work Center OEE summary over a caller-supplied UTC window.
/// First slice (issue #180): the Quality factor plus raw confirmation counts,
/// computed at read time from <c>ProductionConfirmation</c> aggregates. Second
/// slice (issue #181): the Availability factor from the Work Center calendar
/// (planned time) and closed downtime events. Third slice (issue #182): the
/// Performance factor from the ordered products' operations
/// (<c>OperationNode.RunTimePerUnitSeconds</c> via the confirmed Production
/// Orders' recipe versions) and the composite OEE. No new tables and no
/// migration.
/// </summary>
public record GetOeeSummaryRequest(
    Guid MachineId,
    DateTime FromUtc,
    DateTime ToUtc) : ITenantRequest<OeeSummaryResponse>;
