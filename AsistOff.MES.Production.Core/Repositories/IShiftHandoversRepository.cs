using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface IShiftHandoversRepository
{
    Task<IReadOnlyCollection<ShiftHandover>> BrowseAsync(Paginator<ShiftHandover> paginator, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<ShiftHandover> predicate, CancellationToken cancellationToken = default);
    Task<ShiftHandover?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ShiftHandover> AddAsync(ShiftHandover entity, CancellationToken cancellationToken = default);
    /// <summary>
    /// Whether a logbook entry already exists for the same shift boundary.
    /// Runs under the tenant global query filter, so cross-tenant boundaries
    /// never collide.
    /// </summary>
    Task<bool> ExistsAsync(Guid machineId, DateTime fromUtc, CancellationToken cancellationToken = default);
}
