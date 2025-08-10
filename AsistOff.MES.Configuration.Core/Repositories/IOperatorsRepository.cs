using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Configuration.Domain.Repositories;

public interface IOperatorsRepository
{
    Task<IReadOnlyCollection<Operator>> BrowseAsync(
        Paginator<Operator> paginator,
        CancellationToken cancellationToken = default);
}