using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Configuration.Domain.Repositories;

public interface IOperatorsRepository
{
    Task<IReadOnlyCollection<Operator>> BrowseAsync(
        Paginator<Operator> paginator,
        CancellationToken cancellationToken = default);
    
    Task<Operator?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Operator> AddAsync(Operator operatorEntity, CancellationToken cancellationToken = default);
    Task UpdateAsync(Operator operatorEntity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}