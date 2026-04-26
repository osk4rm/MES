using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Configuration.Infrastructure.Repositories;

internal sealed class SkillsRepository(DefaultContext context) : ISkillsRepository
{
    public async Task<IReadOnlyCollection<Skill>> BrowseAsync(Paginator<Skill> paginator, CancellationToken cancellationToken = default)
    {
        return await context.Set<Skill>()
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(ExpressionStarter<Skill> predicate, CancellationToken cancellationToken = default)
    {
        return await context.Set<Skill>().Where(predicate).CountAsync(cancellationToken);
    }

    public async Task<Skill?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Set<Skill>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var q = context.Set<Skill>().Where(x => x.Code == code);
        if (excludeId.HasValue)
            q = q.Where(x => x.Id != excludeId.Value);
        return await q.AnyAsync(cancellationToken);
    }

    public async Task<Skill> AddAsync(Skill skill, CancellationToken cancellationToken = default)
    {
        context.Set<Skill>().Add(skill);
        await context.SaveChangesAsync(cancellationToken);
        return skill;
    }

    public async Task UpdateAsync(Skill skill, CancellationToken cancellationToken = default)
    {
        context.Set<Skill>().Update(skill);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<Skill>()
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
