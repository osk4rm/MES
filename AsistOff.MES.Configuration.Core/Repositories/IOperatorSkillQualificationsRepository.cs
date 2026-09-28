using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Configuration.Domain.Repositories;

public interface IOperatorSkillQualificationsRepository
{
    Task<IReadOnlyCollection<OperatorSkillQualification>> BrowseAsync(
        Paginator<OperatorSkillQualification> paginator,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        ExpressionStarter<OperatorSkillQualification> predicate,
        CancellationToken cancellationToken = default);

    Task<OperatorSkillQualification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        Guid operatorId,
        Guid skillId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Skill codes held by one operator, resolved read-time through the
    /// Skills table (tenant-filtered like every other read).
    /// </summary>
    Task<IReadOnlyCollection<string>> ListSkillCodesForOperatorAsync(
        Guid operatorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Skill codes held by each of the given operators, keyed by operator id.
    /// Operators without qualifications map to an empty set.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<string>>> ListSkillCodesForOperatorsAsync(
        IReadOnlyCollection<Guid> operatorIds,
        CancellationToken cancellationToken = default);

    Task<OperatorSkillQualification> AddAsync(
        OperatorSkillQualification qualification,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
