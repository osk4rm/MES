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

        // Load every operation in the version *with* its dependency edges so
        // that cycle detection sees the whole DAG (lazy loading is intentionally
        // not enabled — without Include, sibling edges would be silently empty).
        var versionOps = await operationsRepository.ListForVersionWithDependenciesAsync(version.Id, cancellationToken);
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

        // Whole-graph cycle detection: take every existing edge in the version,
        // swap in the new edge set for the target operation, and run DFS.
        var allEdges = versionOps
            .SelectMany(o => o.Dependencies.Select(d => (Successor: o.Id, Predecessor: d.PredecessorOperationNodeId)))
            .Where(e => e.Successor != op.Id)
            .ToList();

        allEdges.AddRange(request.Dependencies.Select(d => (op.Id, d.PredecessorOperationId)));

        if (HasCycle(versionOpIds, allEdges))
            throw new ValidationException("Dependencies", "The proposed dependency graph contains a cycle.");

        // Stable diff against the currently tracked edges. Updating in place
        // (rather than Clear() + Add() with new GUIDs) avoids spurious
        // DELETE+INSERT batches that, combined with the unique index
        // (OperationNodeId, PredecessorOperationNodeId), produced
        // DbUpdateConcurrencyException when SaveChanges saw 0 rows affected.
        var existingByPredecessor = op.Dependencies.ToDictionary(d => d.PredecessorOperationNodeId);
        var requestedPredecessors = request.Dependencies.Select(d => d.PredecessorOperationId).ToHashSet();

        // 1) Remove edges that are no longer requested.
        foreach (var existing in op.Dependencies.ToList())
        {
            if (!requestedPredecessors.Contains(existing.PredecessorOperationNodeId))
                op.Dependencies.Remove(existing);
        }

        // 2) Update changed edges in place; insert truly new ones.
        foreach (var dep in request.Dependencies)
        {
            if (existingByPredecessor.TryGetValue(dep.PredecessorOperationId, out var existing))
            {
                existing.DependencyType = dep.DependencyType;
                existing.LagMinutes = dep.LagMinutes;
            }
            else
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
        }

        await operationsRepository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>DFS-based cycle detection over a directed graph.</summary>
    internal static bool HasCycle(HashSet<Guid> nodes, IReadOnlyCollection<(Guid Successor, Guid Predecessor)> edges)
    {
        var successors = edges
            .GroupBy(e => e.Predecessor)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Successor).ToList());

        var state = new Dictionary<Guid, int>();

        foreach (var node in nodes)
        {
            if (state.ContainsKey(node)) continue;
            if (Dfs(node, successors, state)) return true;
        }

        return false;
    }

    private const int Visiting = 1;
    private const int Visited = 2;

    private static bool Dfs(Guid node, Dictionary<Guid, List<Guid>> successors, Dictionary<Guid, int> state)
    {
        state[node] = Visiting;
        if (successors.TryGetValue(node, out var outgoing))
        {
            foreach (var next in outgoing)
            {
                if (!state.TryGetValue(next, out var s))
                {
                    if (Dfs(next, successors, state)) return true;
                }
                else if (s == Visiting)
                {
                    return true;
                }
            }
        }
        state[node] = Visited;
        return false;
    }
}
