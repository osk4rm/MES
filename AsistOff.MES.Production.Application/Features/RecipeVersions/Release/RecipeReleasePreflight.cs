using AsistOff.MES.Production.Domain.Entities;

namespace AsistOff.MES.Production.Application.Features.RecipeVersions.Release;

/// <summary>
/// Outcome of a single release preflight rule.
/// </summary>
public enum PreflightState
{
    Pass,
    Warn,
    Fail
}

/// <summary>
/// One evaluated release preflight rule (issue #388, finding R-7).
/// </summary>
public record PreflightCheck(string Rule, PreflightState State, string Message);

/// <summary>
/// Release preflight rules for recipe versions (issue #388, finding R-7).
/// Pure evaluation over an already-loaded <see cref="RecipeVersion"/> so it is
/// trivially unit-testable without a database. The
/// <see cref="ReleaseRecipeVersionRequestHandler"/> runs the same rules
/// server-side so the checklist cannot be bypassed from the API; the frontend
/// mirrors them in <c>releaseChecklist.ts</c> for the pre-release dialog.
/// <para/>
/// Severity contract: <see cref="PreflightState.Fail"/> blocks the release
/// (surfaced as HTTP 400), <see cref="PreflightState.Warn"/> is shown in the
/// dialog but does not block. Uncertainty is always warn, never fail:
/// BOM/output product ids and warehouse ids are opaque references (older
/// data and movement-preview helpers use ids without a backing Product or
/// Warehouse row), so a missing product or an unknown warehouse only warns
/// and lets the release proceed; only a positively inactive product blocks.
/// This mirrors the frontend <c>releaseChecklist.ts</c> contract.
/// <list type="bullet">
/// <item><c>operations</c> — fail when the version has no operations.</item>
/// <item><c>outputs</c> — warn when no operation output is defined.</item>
/// <item><c>products</c> — fail when a BOM/output product is inactive; warn when a referenced product no longer exists (unverifiable).</item>
/// <item><c>warehouses</c> — warn on unknown warehouse references or when unset; never fails.</item>
/// <item><c>validity</c> — fail when ValidFrom is later than ValidTo.</item>
/// <item><c>dependencies</c> — fail on self-references, cross-version predecessors or cycles.</item>
/// </list>
/// </summary>
public static class RecipeReleasePreflight
{
    public const string OperationsRule = "operations";
    public const string OutputsRule = "outputs";
    public const string ProductsRule = "products";
    public const string WarehousesRule = "warehouses";
    public const string ValidityRule = "validity";
    public const string DependenciesRule = "dependencies";

    /// <param name="version">Fully loaded version (operations with dependencies, BOM items and outputs).</param>
    /// <param name="productActiveById">Active flag per referenced product id; absent entries mean the product no longer exists.</param>
    /// <param name="knownWarehouseIds">Ids of all warehouses visible to the current tenant.</param>
    public static IReadOnlyList<PreflightCheck> Evaluate(
        RecipeVersion version,
        IReadOnlyDictionary<Guid, bool> productActiveById,
        IReadOnlySet<Guid> knownWarehouseIds)
    {
        var checks = new List<PreflightCheck>(6)
        {
            CheckOperations(version),
            CheckOutputs(version),
            CheckValidity(version),
            CheckDependencies(version),
            CheckProducts(version, productActiveById),
            CheckWarehouses(version, knownWarehouseIds)
        };
        return checks;
    }

    public static bool HasFailures(IEnumerable<PreflightCheck> checks)
        => checks.Any(c => c.State == PreflightState.Fail);

    private static PreflightCheck CheckOperations(RecipeVersion version)
        => version.Operations.Count == 0
            ? new PreflightCheck(OperationsRule, PreflightState.Fail,
                "A recipe version must contain at least one operation before it can be released.")
            : new PreflightCheck(OperationsRule, PreflightState.Pass,
                $"{version.Operations.Count} operation(s) defined.");

    private static PreflightCheck CheckOutputs(RecipeVersion version)
    {
        var total = version.Operations.Sum(o => o.Outputs.Count);
        return total == 0
            ? new PreflightCheck(OutputsRule, PreflightState.Warn,
                "No operation outputs are defined. The version releases nothing trackable — add at least one output.")
            : new PreflightCheck(OutputsRule, PreflightState.Pass,
                $"{total} output(s) defined.");
    }

    private static PreflightCheck CheckValidity(RecipeVersion version)
        => version.ValidFrom.HasValue && version.ValidTo.HasValue && version.ValidFrom > version.ValidTo
            ? new PreflightCheck(ValidityRule, PreflightState.Fail,
                "ValidFrom must not be later than ValidTo.")
            : new PreflightCheck(ValidityRule, PreflightState.Pass,
                "Validity dates are coherent.");

    private static PreflightCheck CheckDependencies(RecipeVersion version)
    {
        var opIds = version.Operations.Select(o => o.Id).ToHashSet();

        foreach (var op in version.Operations)
        {
            foreach (var dep in op.Dependencies)
            {
                if (dep.PredecessorOperationNodeId == op.Id)
                    return new PreflightCheck(DependenciesRule, PreflightState.Fail,
                        $"Operation {op.Code} cannot depend on itself.");
                if (!opIds.Contains(dep.PredecessorOperationNodeId))
                    return new PreflightCheck(DependenciesRule, PreflightState.Fail,
                        $"Operation {op.Code} references a predecessor outside this recipe version.");
            }
        }

        return HasCycle(opIds, version.Operations)
            ? new PreflightCheck(DependenciesRule, PreflightState.Fail,
                "The operation dependency graph contains a cycle.")
            : new PreflightCheck(DependenciesRule, PreflightState.Pass,
                "Dependencies are acyclic.");
    }

    private static PreflightCheck CheckProducts(
        RecipeVersion version,
        IReadOnlyDictionary<Guid, bool> productActiveById)
    {
        var inactive = new List<string>();
        var missing = 0;
        var seen = new HashSet<Guid>();

        foreach (var op in version.Operations)
        {
            foreach (var productId in op.BomItems.Select(b => b.ProductId)
                         .Concat(op.Outputs.Select(o => o.ProductId)))
            {
                if (!seen.Add(productId)) continue;
                if (!productActiveById.TryGetValue(productId, out var active))
                    missing++;
                else if (!active)
                    inactive.Add($"referenced product {productId} is inactive");
            }
        }

        if (inactive.Count > 0)
            return new PreflightCheck(ProductsRule, PreflightState.Fail,
                "BOM/output products are not order-ready: " + string.Join("; ", inactive) + ".");

        // Missing products are unverifiable (opaque ids without a backing row),
        // so they warn like the frontend checklist instead of blocking.
        if (missing > 0)
            return new PreflightCheck(ProductsRule, PreflightState.Warn,
                $"{missing} referenced product(s) could not be verified and may no longer exist.");

        return new PreflightCheck(ProductsRule, PreflightState.Pass,
            "All BOM/output products are active.");
    }

    private static PreflightCheck CheckWarehouses(
        RecipeVersion version,
        IReadOnlySet<Guid> knownWarehouseIds)
    {
        var unknown = 0;
        var unset = 0;

        foreach (var op in version.Operations)
        {
            foreach (var warehouseId in op.BomItems.Select(b => b.PreferredWarehouseId)
                         .Concat(op.Outputs.Select(o => o.PreferredWarehouseId)))
            {
                if (!warehouseId.HasValue) unset++;
                else if (!knownWarehouseIds.Contains(warehouseId.Value)) unknown++;
            }
        }

        // Unknown warehouse ids warn (not fail): BOM/output warehouse
        // references are opaque and the lookup may be incomplete, so only the
        // movement/stock logic resolves them. Mirrors releaseChecklist.ts.
        if (unknown > 0 || unset > 0)
            return new PreflightCheck(WarehousesRule, PreflightState.Warn,
                $"{unset} BOM/output item(s) have no preferred warehouse set; {unknown} reference(s) could not be verified.");

        return new PreflightCheck(WarehousesRule, PreflightState.Pass,
            "Warehouses are set.");
    }

    /// <summary>DFS-based cycle detection over the version's dependency edges.</summary>
    private static bool HasCycle(HashSet<Guid> nodes, ICollection<OperationNode> operations)
    {
        var successors = new Dictionary<Guid, List<Guid>>();
        foreach (var op in operations)
        {
            foreach (var dep in op.Dependencies)
            {
                if (!successors.TryGetValue(dep.PredecessorOperationNodeId, out var list))
                    successors[dep.PredecessorOperationNodeId] = list = [];
                list.Add(op.Id);
            }
        }

        var state = new Dictionary<Guid, int>();
        foreach (var node in nodes)
        {
            if (!state.ContainsKey(node) && Dfs(node, successors, state))
                return true;
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
