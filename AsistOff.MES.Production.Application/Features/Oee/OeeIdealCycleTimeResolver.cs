using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;

namespace AsistOff.MES.Production.Application.Features.Oee;

/// <summary>
/// Resolves the OEE ideal cycle time from routing master data: the minimum
/// positive <c>OperationNode.RunTimePerUnitSeconds</c> across the recipe
/// versions of the confirmed Production Orders in the window. All lookups run
/// under the tenant global query filter, so cross-tenant orders and operations
/// stay invisible and surface as a null ideal (never foreign data).
/// Shared by the summary, snapshot and trend handlers so all three report the
/// same Performance for the same window.
/// </summary>
internal static class OeeIdealCycleTimeResolver
{
    public const string CallerSource = "caller";
    public const string RoutingSource = "routing";

    public static async Task<decimal?> ResolveAsync(
        IReadOnlyCollection<ProductionConfirmation> confirmations,
        IProductionOrdersRepository ordersRepository,
        IOperationNodesRepository operationsRepository,
        CancellationToken cancellationToken)
    {
        if (confirmations.Count == 0)
            return null;

        var versionIds = new HashSet<Guid>();
        foreach (var orderId in confirmations.Select(c => c.ProductionOrderId).Distinct())
        {
            var order = await ordersRepository.GetAsync(orderId, cancellationToken);
            if (order is not null)
                versionIds.Add(order.RecipeVersionId);
        }

        decimal? best = null;
        foreach (var versionId in versionIds)
        {
            var operations = await operationsRepository.ListForVersionAsync(versionId, cancellationToken);
            foreach (var operation in operations)
            {
                if (operation.RunTimePerUnitSeconds is { } seconds && seconds > 0
                    && (best is null || seconds < best))
                    best = seconds;
            }
        }

        return best;
    }
}
