using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Production.Infrastructure.Repositories;

internal sealed class OperationDependenciesRepository(DefaultContext context) : IOperationDependenciesRepository
{
    public async Task<IReadOnlyCollection<OperationDependency>> ListForOperationAsync(
        Guid operationNodeId, CancellationToken cancellationToken = default)
        => await context.Set<OperationDependency>()
            .Where(d => d.OperationNodeId == operationNodeId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<DependencyEdge>> ListEdgesForVersionAsync(
        Guid recipeVersionId, CancellationToken cancellationToken = default)
        => await context.Set<OperationDependency>()
            .Where(d => d.RecipeVersionId == recipeVersionId)
            .Select(d => new DependencyEdge(d.OperationNodeId, d.PredecessorOperationNodeId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> ListOperationIdsForVersionAsync(
        Guid recipeVersionId, CancellationToken cancellationToken = default)
        => await context.Set<OperationNode>()
            .Where(o => o.RecipeVersionId == recipeVersionId)
            .Select(o => o.Id)
            .ToListAsync(cancellationToken);

    public void Add(OperationDependency entity) => context.Set<OperationDependency>().Add(entity);

    public void Remove(OperationDependency entity) => context.Set<OperationDependency>().Remove(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);
}
