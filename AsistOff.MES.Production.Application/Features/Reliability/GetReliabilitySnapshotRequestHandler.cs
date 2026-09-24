using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Reliability;

internal sealed class GetReliabilitySnapshotRequestHandler(
    IMachinesRepository machinesRepository,
    IDowntimeEventsRepository downtimeEventsRepository,
    IMaintenanceWorkOrdersRepository maintenanceWorkOrdersRepository)
    : IRequestHandler<GetReliabilitySnapshotRequest, ReliabilitySnapshotResponse>
{
    private const double MaxWindowDays = 93;

    public async Task<ReliabilitySnapshotResponse> Handle(
        GetReliabilitySnapshotRequest request, CancellationToken cancellationToken)
    {
        if (request.MachineId == Guid.Empty)
            throw new ValidationException(nameof(request.MachineId), "Machine is required.");

        if (request.FromUtc == default || request.ToUtc == default)
            throw new ValidationException(nameof(request.FromUtc), "Time window is required.");

        var fromUtc = request.FromUtc.ToUniversalTime();
        var toUtc = request.ToUtc.ToUniversalTime();

        if (fromUtc >= toUtc)
            throw new ValidationException(nameof(request.FromUtc), "FromUtc must be before ToUtc.");

        if ((toUtc - fromUtc).TotalDays > MaxWindowDays)
            throw new ValidationException(nameof(request.ToUtc), "Time window cannot exceed 93 days.");

        // The global tenant query filter scopes the machine lookup to the
        // caller tenant, so unknown and cross-tenant ids both yield 404.
        _ = await machinesRepository.GetByIdAsync(request.MachineId, cancellationToken)
            ?? throw new NotFoundException("Machine", request.MachineId);

        var downtimes = await downtimeEventsRepository.ListOverlappingAsync(
            request.MachineId, fromUtc, toUtc, cancellationToken);

        // Only closed-event overlap counts; open events are ignored.
        var closedOverlaps = downtimes
            .Where(e => e.EndedAt.HasValue)
            .Select(e => OverlapMinutes(e.StartedAt, e.EndedAt!.Value, fromUtc, toUtc))
            .ToList();

        var failureCount = closedOverlaps.Count;
        var totalDowntimeMinutes = Round2(closedOverlaps.Sum());

        var windowMinutes = (toUtc - fromUtc).TotalMinutes;
        var uptimeMinutes = windowMinutes - totalDowntimeMinutes;

        double? mtbfMinutes = failureCount > 0
            ? Round2(uptimeMinutes / failureCount)
            : null;
        double? mttrMinutes = failureCount > 0
            ? Round2(totalDowntimeMinutes / failureCount)
            : null;

        var repairs = await maintenanceWorkOrdersRepository.ListDoneInWindowAsync(
            request.MachineId, fromUtc, toUtc, cancellationToken);

        // The repository already filters Done + CompletedAt in window; ignore
        // Open/InProgress/Cancelled defensively so non-Done rows never count.
        var doneRepairs = repairs
            .Where(r => r.Status == MaintenanceWorkOrderStatus.Done && r.CompletedAt.HasValue)
            .ToList();

        var repairCount = doneRepairs.Count;
        double? avgRepairMinutes = repairCount > 0
            ? Round2(doneRepairs.Average(r =>
                (r.CompletedAt!.Value - (r.StartedAt ?? r.ReportedAt)).TotalMinutes))
            : null;

        return new ReliabilitySnapshotResponse(
            request.MachineId,
            fromUtc,
            toUtc,
            failureCount,
            repairCount,
            windowMinutes,
            uptimeMinutes,
            totalDowntimeMinutes,
            mtbfMinutes,
            mttrMinutes,
            avgRepairMinutes);
    }

    internal static double OverlapMinutes(DateTime start, DateTime end, DateTime from, DateTime to)
    {
        var overlapStart = start > from ? start : from;
        var overlapEnd = end < to ? end : to;

        return overlapEnd > overlapStart ? (overlapEnd - overlapStart).TotalMinutes : 0;
    }

    internal static double Round2(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
