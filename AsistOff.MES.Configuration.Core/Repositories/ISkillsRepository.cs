using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Configuration.Domain.Repositories;

public interface ISkillsRepository
{
    Task<IReadOnlyCollection<Skill>> BrowseAsync(Paginator<Skill> paginator, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<Skill> predicate, CancellationToken cancellationToken = default);
    Task<Skill?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>
    /// Skills whose code matches any of the given codes, matched
    /// case-insensitively to agree with the OrdinalIgnoreCase gating
    /// comparison (issue #397). Used by the operator
    /// skill gating (issue #397) to resolve recipe resource-requirement
    /// capability strings down to real skill rows; legacy free-text
    /// capabilities simply resolve to nothing and are not enforced.
    /// </summary>
    Task<IReadOnlyCollection<Skill>> ListByCodesAsync(
        IReadOnlyCollection<string> codes, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default);
    Task<Skill> AddAsync(Skill skill, CancellationToken cancellationToken = default);
    Task UpdateAsync(Skill skill, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
