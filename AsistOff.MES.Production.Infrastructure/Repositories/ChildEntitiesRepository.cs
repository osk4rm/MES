using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Production.Infrastructure.Repositories;

internal sealed class ChildEntitiesRepository(DefaultContext context) : IChildEntitiesRepository
{
    public Task<BomItem?> GetBomItemAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<BomItem>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<OperationOutput?> GetOperationOutputAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<OperationOutput>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<ResourceRequirement?> GetResourceRequirementAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<ResourceRequirement>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task AddBomItemAsync(BomItem entity, CancellationToken cancellationToken = default)
    {
        context.Set<BomItem>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddOperationOutputAsync(OperationOutput entity, CancellationToken cancellationToken = default)
    {
        context.Set<OperationOutput>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddResourceRequirementAsync(ResourceRequirement entity, CancellationToken cancellationToken = default)
    {
        context.Set<ResourceRequirement>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int?> GetMaxBomItemSortIndexAsync(Guid operationNodeId, CancellationToken cancellationToken = default)
        => await context.Set<BomItem>()
            .Where(x => x.OperationNodeId == operationNodeId)
            .Select(x => (int?)x.SortIndex)
            .MaxAsync(cancellationToken);

    public async Task RemoveBomItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<BomItem>().Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task RemoveOperationOutputAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<OperationOutput>().Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task RemoveResourceRequirementAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<ResourceRequirement>().Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);
}
