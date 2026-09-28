using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Configuration.Infrastructure.Repositories;

internal sealed class OperatorSkillQualificationsRepository(DefaultContext context)
    : IOperatorSkillQualificationsRepository
{
    public async Task<IReadOnlyCollection<OperatorSkillQualification>> BrowseAsync(
        Paginator<OperatorSkillQualification> paginator, CancellationToken cancellationToken = default)
    {
        return await context.Set<OperatorSkillQualification>()
            .Include(x => x.Operator)
            .Include(x => x.Skill)
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(
        ExpressionStarter<OperatorSkillQualification> predicate, CancellationToken cancellationToken = default)
    {
        return await context.Set<OperatorSkillQualification>().Where(predicate).CountAsync(cancellationToken);
    }

    public async Task<OperatorSkillQualification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Set<OperatorSkillQualification>()
            .Include(x => x.Operator)
            .Include(x => x.Skill)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        Guid operatorId, Guid skillId, CancellationToken cancellationToken = default)
    {
        return await context.Set<OperatorSkillQualification>()
            .AnyAsync(x => x.OperatorId == operatorId && x.SkillId == skillId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<string>> ListSkillCodesForOperatorAsync(
        Guid operatorId, CancellationToken cancellationToken = default)
    {
        var codes = await context.Set<OperatorSkillQualification>()
            .Where(x => x.OperatorId == operatorId)
            .Join(context.Set<Skill>(),
                q => q.SkillId,
                s => s.Id,
                (_, s) => s.Code)
            .ToListAsync(cancellationToken);
        return codes;
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<string>>> ListSkillCodesForOperatorsAsync(
        IReadOnlyCollection<Guid> operatorIds, CancellationToken cancellationToken = default)
    {
        var result = operatorIds.Distinct().ToDictionary(id => id, _ => (IReadOnlyCollection<string>)[]);
        if (operatorIds.Count == 0)
            return result;

        var rows = await context.Set<OperatorSkillQualification>()
            .Where(x => operatorIds.Contains(x.OperatorId))
            .Join(context.Set<Skill>(),
                q => q.SkillId,
                s => s.Id,
                (q, s) => new { q.OperatorId, s.Code })
            .ToListAsync(cancellationToken);

        foreach (var group in rows.GroupBy(x => x.OperatorId))
            result[group.Key] = group.Select(x => x.Code).Distinct().ToList();

        return result;
    }

    public async Task<OperatorSkillQualification> AddAsync(
        OperatorSkillQualification qualification, CancellationToken cancellationToken = default)
    {
        context.Set<OperatorSkillQualification>().Add(qualification);
        await context.SaveChangesAsync(cancellationToken);
        return qualification;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<OperatorSkillQualification>()
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
