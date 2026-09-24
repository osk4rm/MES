using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Configuration.Domain.Repositories;

public interface IOperatorShiftAssignmentsRepository
{
    Task<IReadOnlyCollection<OperatorShiftAssignment>> BrowseAsync(
        Paginator<OperatorShiftAssignment> paginator,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        ExpressionStarter<OperatorShiftAssignment> predicate,
        CancellationToken cancellationToken = default);

    Task<OperatorShiftAssignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        Guid operatorId,
        Guid shiftId,
        DateOnly date,
        CancellationToken cancellationToken = default);

    Task<OperatorShiftAssignment> AddAsync(
        OperatorShiftAssignment assignment,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
