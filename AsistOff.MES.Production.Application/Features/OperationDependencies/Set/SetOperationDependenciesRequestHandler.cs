using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.OperationDependencies.Set;

internal sealed class SetOperationDependenciesRequestHandler(
    IOperationNodesRepository operationsRepository,
    IRecipeVersionsRepository versionsRepository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext)
    : IRequestHandler<SetOperationDependenciesRequest>
{
    public async Task Handle(SetOperationDependenciesRequest request, CancellationToken cancellationToken)
    {
        var op = await operationsRepository.GetWithDetailsAsync(request.OperationId, cancellationToken)
            ?? throw new NotFoundException("OperationNode", request.OperationId);

        var version = await VersionGuard.EnsureDraftAsync(versionsRepository, op.RecipeVersionId, cancellationToken);

        var versionOps = await operationsRepository.ListForVersionAsync(version.Id, cancellationToken);
        var versionOpIds = versionOps.Select(o => o.Id).ToHashSet();

        // Validate predecessor identities and reject self-loops / duplicates.
        var seen = new HashSet<Guid>();
        foreach (var dep in request.Dependencies)
        {
            if (dep.PredecessorOperationId == op.Id)
                throw new ValidationException("Dependencies", "An operation cannot depend on itself.");
            if (!versionOpIds.Contains(dep.PredecessorOperationId))
                throw new ValidationException("Dependencies", $"Predecessor {dep.PredecessorOperationId} does not belong to this recipe version.");
            if (!seen.Add(dep.PredecessorOperationId))
                throw new ValidationException("Dependencies", "Duplicate predecessor entries are not allowed.");
        }

        // Load every edge in the version, swap in the new edge set for the
        // target operation, and perform cycle detection across the whole DAG.
        var allEdges = versionOps
            .SelectMany(o => o.Dependencies.Select(d => (Successor: o.Id, Predecessor: d.PredecessorOperationNodeId)))
            .Where(e => e.Successor != op.Id)
            .ToList();

        allEdges.AddRange(request.Dependencies.Select(d => (op.Id, d.PredecessorOperationId)));

        if (HasCycle(versionOpIds, allEdges))
            throw new ValidationException("Dependencies", "The proposed dependency graph contains a cycle.");

        op.Dependencies.Clear();
        foreach (var dep in request.Dependencies)
        {
            op.Dependencies.Add(new OperationDependency
            {
                Id = guidProvider.NewGuid(),
                TenantId = tenantContext.TenantId,
                RecipeVersionId = version.Id,
                OperationNodeId = op.Id,
                PredecessorOperationNodeId = dep.PredecessorOperationId,
                DependencyType = dep.DependencyType,
                LagMinutes = dep.LagMinutes
            });
        }

        await operationsRepository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>DFS-based cycle detection over a directed graph.</summary>
    internal static bool HasCycle(HashSet<Guid> nodes, IReadOnlyCollection<(Guid Successor, Guid Predecessor)> edges)
    {
        var successors = edges
            .GroupBy(e => e.Predecessor)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Successor).ToList());

        const int VisitingState = 1;
        var state = new Dictionary<Guid, int>();

        foreach (var node in nodes)
        {
            if (state.ContainsKey(node)) continue;
            if (Dfs(node, successors, state)) return true;
        }

        return false;
    }

    private static bool Dfs(Guid node, Dictionary<Guid, List<Guid>> successors, Dictionary<Guid, int> state)
    {
        state[node] = VisitingState;
        if (successors.TryGetValue(node, out var outgoing))
        {
            foreach (var next in outgoing)
            {
                if (!state.TryGetValue(next, out var s))
                {
                    if (Dfs(next, successors, state)) return true;
                }
                else if (s == VisitingState)
                {
                    return true;
                }
            }
        }
        state[node] = 2; // fully visited
        return false;
    }
}
