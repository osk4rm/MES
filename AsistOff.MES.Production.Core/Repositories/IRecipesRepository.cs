using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface IRecipesRepository
{
    Task<IReadOnlyCollection<Recipe>> BrowseAsync(Paginator<Recipe> paginator, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<Recipe> predicate, CancellationToken cancellationToken = default);
    Task<Recipe?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Recipe?> GetWithVersionsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Recipe> AddAsync(Recipe entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(Recipe entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default);
}
