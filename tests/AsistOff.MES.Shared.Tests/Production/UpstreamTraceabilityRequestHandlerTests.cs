using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.LotGenealogy.Upstream;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class UpstreamTraceabilityRequestHandlerTests
{
    private readonly Mock<ILotGenealogyEdgesRepository> _edges = new();
    private readonly Mock<ILotsRepository> _lots = new();
    private readonly Mock<IProductionOrdersRepository> _orders = new();

    private GetUpstreamTraceabilityRequestHandler CreateSut() =>
        new(_edges.Object, _lots.Object, _orders.Object);

    private static Lot NewLot(Guid id, string code) => new()
    {
        Id = id,
        TenantId = Guid.NewGuid(),
        Code = code,
        ProductId = Guid.NewGuid(),
        MeasureUnitId = Guid.NewGuid(),
        Quantity = 100m,
        Status = LotStatus.Available,
        CreatedAt = new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc)
    };

    private static ProductionOrder NewOrder(Guid id, string code) => new()
    {
        Id = id,
        Code = code,
        ProductId = Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        RecipeVersionId = Guid.NewGuid(),
        PlannedQuantity = 100m,
        Status = ProductionOrderStatus.Released,
        ReleasedAt = new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc)
    };

    private static LotGenealogyEdge NewEdge(
        Guid consumedLotId, Guid producedLotId, Guid orderId, DateTime occurredAt, decimal quantity = 5m) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        ConsumedLotId = consumedLotId,
        ProducedLotId = producedLotId,
        ProductionOrderId = orderId,
        MachineId = Guid.NewGuid(),
        ConsumedQuantity = quantity,
        OccurredAt = occurredAt,
        CreatedAt = occurredAt
    };

    private void SetupLotsAndOrders(
        IEnumerable<Lot> lots, IEnumerable<ProductionOrder> orders, Guid rootId)
    {
        var lotsById = lots.ToDictionary(l => l.Id);
        var ordersById = orders.ToDictionary(o => o.Id);

        if (!lotsById.ContainsKey(rootId))
            throw new InvalidOperationException("Root lot must be part of the lots collection.");

        _lots.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                lotsById.TryGetValue(id, out var lot) ? lot : null);

        _orders.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                ordersById.TryGetValue(id, out var order) ? order : null);
    }

    private void SetupUpstreamEdges(IEnumerable<LotGenealogyEdge> edges)
    {
        var all = edges.ToList();
        _edges.Setup(r => r.ListByProducedLotIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                (IReadOnlyCollection<LotGenealogyEdge>)all.Where(e => ids.Contains(e.ProducedLotId)).ToList());
    }

    [Fact]
    public void Request_ImplementsTenantRequest()
    {
        typeof(GetUpstreamTraceabilityRequest).Should().Implement<ITenantRequest<AsistOff.MES.Production.Application.Features.LotGenealogy.LotTraceabilityResponse>>();
    }

    [Fact]
    public async Task Handle_TwoLevelChain_ReturnsBothLevelsWithDetails()
    {
        // Arrange: finished -> mid -> raw (two levels upstream of finished).
        var rawId = Guid.NewGuid();
        var midId = Guid.NewGuid();
        var finishedId = Guid.NewGuid();
        var order1Id = Guid.NewGuid();
        var order2Id = Guid.NewGuid();

        var raw = NewLot(rawId, "RAW-001");
        var mid = NewLot(midId, "MID-001");
        var finished = NewLot(finishedId, "FIN-001");
        var order1 = NewOrder(order1Id, "PO-001");
        var order2 = NewOrder(order2Id, "PO-002");

        var t1 = new DateTime(2026, 9, 24, 9, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc);
        var machine2 = Guid.NewGuid();
        var operator2 = Guid.NewGuid();
        var edge1 = NewEdge(rawId, midId, order1Id, t1);
        var edge2 = NewEdge(midId, finishedId, order2Id, t2);
        edge2.MachineId = machine2;
        edge2.ReportedByOperatorId = operator2;
        edge2.ConsumedQuantity = 7m;

        SetupLotsAndOrders([raw, mid, finished], [order1, order2], finishedId);
        SetupUpstreamEdges([edge1, edge2]);

        // Act
        var result = await CreateSut().Handle(new GetUpstreamTraceabilityRequest(finishedId, null), CancellationToken.None);

        // Assert
        result.RootLotId.Should().Be(finishedId);
        result.RootLotCode.Should().Be("FIN-001");
        result.Truncated.Should().BeFalse();
        result.Nodes.Should().HaveCount(2);

        var level1 = result.Nodes.Single(n => n.LotId == midId);
        level1.Depth.Should().Be(1);
        level1.LotCode.Should().Be("MID-001");
        level1.ConsumedQuantity.Should().Be(7m);
        level1.ProductionOrderCode.Should().Be("PO-002");
        level1.MachineId.Should().Be(machine2);
        level1.ReportedByOperatorId.Should().Be(operator2);
        level1.OccurredAt.Should().Be(t2);

        var level2 = result.Nodes.Single(n => n.LotId == rawId);
        level2.Depth.Should().Be(2);
        level2.LotCode.Should().Be("RAW-001");
        level2.ProductionOrderCode.Should().Be("PO-001");
        level2.OccurredAt.Should().Be(t1);
    }

    [Fact]
    public async Task Handle_DiamondMerge_ListsSharedAncestorOnce()
    {
        // Arrange: finished consumes A and B; both A and B consume shared root.
        var sharedId = Guid.NewGuid();
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var finishedId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var shared = NewLot(sharedId, "SHARED");
        var a = NewLot(aId, "A");
        var b = NewLot(bId, "B");
        var finished = NewLot(finishedId, "FIN");
        var order = NewOrder(orderId, "PO-D");

        var t = new DateTime(2026, 9, 24, 9, 0, 0, DateTimeKind.Utc);
        var edges = new[]
        {
            NewEdge(aId, finishedId, orderId, t),
            NewEdge(bId, finishedId, orderId, t.AddMinutes(1)),
            NewEdge(sharedId, aId, orderId, t.AddMinutes(-2)),
            NewEdge(sharedId, bId, orderId, t.AddMinutes(-1))
        };

        SetupLotsAndOrders([shared, a, b, finished], [order], finishedId);
        SetupUpstreamEdges(edges);

        // Act
        var result = await CreateSut().Handle(new GetUpstreamTraceabilityRequest(finishedId, null), CancellationToken.None);

        // Assert
        result.Nodes.Select(n => n.LotId).Should().OnlyHaveUniqueItems();
        result.Nodes.Single(n => n.LotId == sharedId).Depth.Should().Be(2);
        result.Nodes.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_Cycle_TerminatesAndListsEachLotOnce()
    {
        // Arrange: X -> Y -> X cycle reachable upstream of root R (R <- X <- Y <- X ...).
        var xId = Guid.NewGuid();
        var yId = Guid.NewGuid();
        var rootId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var x = NewLot(xId, "X");
        var y = NewLot(yId, "Y");
        var root = NewLot(rootId, "R");
        var order = NewOrder(orderId, "PO-C");

        var t = new DateTime(2026, 9, 24, 9, 0, 0, DateTimeKind.Utc);
        var edges = new[]
        {
            NewEdge(xId, rootId, orderId, t),
            NewEdge(yId, xId, orderId, t.AddMinutes(-1)),
            NewEdge(xId, yId, orderId, t.AddMinutes(-2))
        };

        SetupLotsAndOrders([x, y, root], [order], rootId);
        SetupUpstreamEdges(edges);

        // Act
        var result = await CreateSut().Handle(new GetUpstreamTraceabilityRequest(rootId, 10), CancellationToken.None);

        // Assert
        result.Nodes.Select(n => n.LotId).Should().OnlyHaveUniqueItems();
        result.Nodes.Select(n => n.LotId).Should().BeEquivalentTo([xId, yId]);
    }

    [Fact]
    public async Task Handle_UnknownLot_ThrowsNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _lots.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lot?)null);

        // Act
        var act = () => CreateSut().Handle(new GetUpstreamTraceabilityRequest(id, null), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_CrossTenantRoot_ResolvesToNotFound()
    {
        // Arrange: the global query filter hides foreign-tenant lots, so the
        // repository returns null and the handler maps it to 404.
        var id = Guid.NewGuid();
        _lots.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lot?)null);

        // Act
        var act = () => CreateSut().Handle(new GetUpstreamTraceabilityRequest(id, null), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _edges.Verify(r => r.ListByProducedLotIdsAsync(
            It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    [InlineData(100)]
    public async Task Handle_InvalidDepth_ThrowsValidationException(int maxDepth)
    {
        // Arrange
        var act = () => CreateSut().Handle(
            new GetUpstreamTraceabilityRequest(Guid.NewGuid(), maxDepth), CancellationToken.None);

        // Act + Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_DepthCap_StopsAtRequestedDepth()
    {
        // Arrange: linear chain of 4 upstream levels, request depth 2.
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var lots = ids.Select((id, i) => NewLot(id, $"LOT-{i}")).ToArray();
        var order = NewOrder(Guid.NewGuid(), "PO");
        var t = new DateTime(2026, 9, 24, 9, 0, 0, DateTimeKind.Utc);
        var edges = Enumerable.Range(0, 4)
            .Select(i => NewEdge(ids[i], ids[i + 1], order.Id, t.AddMinutes(i)))
            .ToArray();

        SetupLotsAndOrders(lots, [order], ids[4]);
        SetupUpstreamEdges(edges);

        // Act
        var result = await CreateSut().Handle(new GetUpstreamTraceabilityRequest(ids[4], 2), CancellationToken.None);

        // Assert
        result.Nodes.Should().HaveCount(2);
        result.Nodes.Max(n => n.Depth).Should().Be(2);
        result.Truncated.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_OverCap_Returns500NodesWithTruncatedFlag()
    {
        // Arrange: root consumed from 600 direct components (depth 1 fan-in).
        var rootId = Guid.NewGuid();
        var root = NewLot(rootId, "ROOT");
        var order = NewOrder(Guid.NewGuid(), "PO");
        var t = new DateTime(2026, 9, 24, 9, 0, 0, DateTimeKind.Utc);

        var components = Enumerable.Range(0, 600)
            .Select(i => NewLot(Guid.NewGuid(), $"C-{i:000}"))
            .ToArray();
        var edges = components
            .Select((c, i) => NewEdge(c.Id, rootId, order.Id, t.AddSeconds(i)))
            .ToArray();

        SetupLotsAndOrders([root, .. components], [order], rootId);
        SetupUpstreamEdges(edges);

        // Act
        var result = await CreateSut().Handle(new GetUpstreamTraceabilityRequest(rootId, 10), CancellationToken.None);

        // Assert
        result.Nodes.Should().HaveCount(500);
        result.Truncated.Should().BeTrue();
    }
}
