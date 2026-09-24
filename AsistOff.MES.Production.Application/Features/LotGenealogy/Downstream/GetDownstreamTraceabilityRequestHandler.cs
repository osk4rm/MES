using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.LotGenealogy.Downstream;

internal sealed class GetDownstreamTraceabilityRequestHandler(
    ILotGenealogyEdgesRepository edgesRepository,
    ILotsRepository lotsRepository,
    IProductionOrdersRepository ordersRepository)
    : IRequestHandler<GetDownstreamTraceabilityRequest, LotTraceabilityResponse>
{
    internal const int MaxNodes = 500;

    public async Task<LotTraceabilityResponse> Handle(
        GetDownstreamTraceabilityRequest request, CancellationToken cancellationToken)
    {
        var maxDepth = request.MaxDepth ?? 5;
        if (maxDepth is < 1 or > 10)
            throw new ValidationException(nameof(request.MaxDepth), "Max depth must be between 1 and 10.");

        var root = await lotsRepository.GetAsync(request.LotId, cancellationToken)
            ?? throw new NotFoundException("Lot", request.LotId);

        var visited = new HashSet<Guid> { root.Id };
        var nodes = new List<LotTraceabilityNodeResponse>();
        var frontier = new List<Guid> { root.Id };
        var truncated = false;

        var lotCache = new Dictionary<Guid, Lot?>();
        var orderCache = new Dictionary<Guid, ProductionOrder?>();

        for (var depth = 1; depth <= maxDepth && !truncated; depth++)
        {
            var edges = await edgesRepository.ListByConsumedLotIdsAsync(frontier, cancellationToken);
            var nextFrontier = new List<Guid>();

            foreach (var edge in edges.OrderBy(e => e.OccurredAt))
            {
                if (!visited.Add(edge.ProducedLotId))
                    continue;

                if (nodes.Count >= MaxNodes)
                {
                    truncated = true;
                    break;
                }

                var lot = await GetLotCachedAsync(edge.ProducedLotId, lotCache, cancellationToken);
                if (lot is null)
                {
                    visited.Remove(edge.ProducedLotId);
                    continue;
                }

                var order = await GetOrderCachedAsync(edge.ProductionOrderId, orderCache, cancellationToken);
                if (order is null)
                {
                    visited.Remove(edge.ProducedLotId);
                    continue;
                }

                nextFrontier.Add(edge.ProducedLotId);
                nodes.Add(new LotTraceabilityNodeResponse(
                    edge.ProducedLotId,
                    lot.Code,
                    lot.ProductId,
                    depth,
                    edge.ConsumedQuantity,
                    order.Id,
                    order.Code,
                    edge.MachineId,
                    edge.ReportedByOperatorId,
                    edge.OccurredAt));
            }

            if (truncated)
                break;

            if (nextFrontier.Count == 0)
                break;

            frontier = nextFrontier.Distinct().ToList();

            // Exact-cap edge case: the level filled the cap precisely and more
            // levels remain. Peek one level ahead; flag truncation only when
            // further unvisited lots are actually reachable.
            if (nodes.Count >= MaxNodes && depth < maxDepth)
            {
                var peek = await edgesRepository.ListByConsumedLotIdsAsync(frontier, cancellationToken);
                if (peek.Any(e => !visited.Contains(e.ProducedLotId)))
                    truncated = true;
                break;
            }
        }

        return new LotTraceabilityResponse(root.Id, root.Code, nodes, truncated);
    }

    private async Task<Lot?> GetLotCachedAsync(
        Guid lotId, Dictionary<Guid, Lot?> cache, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(lotId, out var cached))
            return cached;

        var lot = await lotsRepository.GetAsync(lotId, cancellationToken);
        cache[lotId] = lot;
        return lot;
    }

    private async Task<ProductionOrder?> GetOrderCachedAsync(
        Guid orderId, Dictionary<Guid, ProductionOrder?> cache, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(orderId, out var cached))
            return cached;

        var order = await ordersRepository.GetAsync(orderId, cancellationToken);
        cache[orderId] = order;
        return order;
    }
}
