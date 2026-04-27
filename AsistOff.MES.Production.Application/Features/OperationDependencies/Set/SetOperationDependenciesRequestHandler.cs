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
    IOperationDependenciesRepository dependenciesRepository,
    IRecipeVersionsRepository versionsRepository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext)
    : IRequestHandler<SetOperationDependenciesRequest>
{
    public async Task Handle(SetOperationDependenciesRequest request, CancellationToken cancellationToken)
    {
        // Lightweight load: we only need the operation's RecipeVersionId for the draft
        // guard. Do *not* load the operation with eager Includes — mutating the navigation
        // collection of a tracked principal under split-query loading was the original
        // source of DbUpdateConcurrencyException. All edge mutation here flows through
        // IOperationDependenciesRepository (DbSet.Add / DbSet.Remove on tracked entities).
        var op = await operationsRepository.GetAsync(request.OperationId, cancellationToken)
            ?? throw new NotFoundException("OperationNode", request.OperationId);

        var version = await VersionGuard.EnsureDraftAsync(versionsRepository, op.RecipeVersionId, cancellationToken);

        var versionOpIds = (await dependenciesRepository.ListOperationIdsForVersionAsync(version.Id, cancellationToken))
            .ToHashSet();

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

        // Whole-graph cycle detection: take every existing edge in the version (cheap
        // projection — no entity hydration), swap in the proposed edge set for the
        // target operation, and run DFS.
        var existingVersionEdges = await dependenciesRepository.ListEdgesForVersionAsync(version.Id, cancellationToken);
        var allEdges = existingVersionEdges
            .Where(e => e.Successor != op.Id)
            .ToList();
        allEdges.AddRange(request.Dependencies.Select(d => new DependencyEdge(op.Id, d.PredecessorOperationId)));

        if (HasCycle(versionOpIds, allEdges))
            throw new ValidationException("Dependencies", "The proposed dependency graph contains a cycle.");

        // Stable diff against the currently tracked edges (loaded for THIS operation only).
        // Updating in place — rather than Clear() + Add() with new GUIDs — avoids spurious
        // DELETE+INSERT batches that, combined with the unique index
        // (OperationNodeId, PredecessorOperationNodeId), produced DbUpdateConcurrencyException
        // when SaveChanges saw 0 rows affected.
        var existingEdges = await dependenciesRepository.ListForOperationAsync(op.Id, cancellationToken);
        var existingByPredecessor = existingEdges.ToDictionary(d => d.PredecessorOperationNodeId);
        var requestedPredecessors = request.Dependencies.Select(d => d.PredecessorOperationId).ToHashSet();

        // 1) Remove edges that are no longer requested.
        foreach (var existing in existingEdges)
        {
            if (!requestedPredecessors.Contains(existing.PredecessorOperationNodeId))
                dependenciesRepository.Remove(existing);
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
                dependenciesRepository.Add(new OperationDependency
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

        await dependenciesRepository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>DFS-based cycle detection over a directed graph.</summary>
    internal static bool HasCycle(HashSet<Guid> nodes, IReadOnlyCollection<DependencyEdge> edges)
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
