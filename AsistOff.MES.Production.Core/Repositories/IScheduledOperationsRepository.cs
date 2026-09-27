using AsistOff.MES.Production.Domain.Entities;

namespace AsistOff.MES.Production.Domain.Repositories;

/// <summary>
/// Repository for <see cref="ScheduledOperation"/> manual schedule overrides
/// (issue #304). The Gantt read-model overlays these rows on the computed
/// schedule; writes (drag/resize) arrive in slice 2/3.
/// </summary>
public interface IScheduledOperationsRepository
{
    /// <summary>
    /// Returns every override for the given orders. Runs under the tenant
    /// global query filter with no change tracking.
    /// </summary>
    Task<IReadOnlyCollection<ScheduledOperation>> ListForOrdersAsync(
        IReadOnlyCollection<Guid> productionOrderIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every override pinned to the given Work Center. Required by the
    /// Gantt reschedule leveling check (issue #305): the overlap scan stays
    /// bounded to one lane instead of reading the whole override table. Runs
    /// under the tenant global query filter with no change tracking.
    /// </summary>
    Task<IReadOnlyCollection<ScheduledOperation>> ListForMachineAsync(
        Guid machineId, CancellationToken cancellationToken = default);

    Task<ScheduledOperation> AddAsync(ScheduledOperation entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(ScheduledOperation entity, CancellationToken cancellationToken = default);
}
