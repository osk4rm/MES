using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.LotGenealogy.Downstream;
using AsistOff.MES.Production.Application.Features.LotGenealogy;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class DownstreamTraceabilityRequestHandlerTests
{
    private readonly Mock<ILotGenealogyEdgesRepository> _edges = new();
    private readonly Mock<ILotsRepository> _lots = new();
    private readonly Mock<IProductionOrdersRepository> _orders = new();

    private GetDownstreamTraceabilityRequestHandler CreateSut() =>
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
        IEnumerable<Lot> lots, IEnumerable<ProductionOrder> orders)
    {
        var lotsById = lots.ToDictionary(l => l.Id);
        var ordersById = orders.ToDictionary(o => o.Id);

        _lots.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                lotsById.TryGetValue(id, out var lot) ? lot : null);

        _orders.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                ordersById.TryGetValue(id, out var order) ? order : null);
    }

    private void SetupDownstreamEdges(IEnumerable<LotGenealogyEdge> edges)
    {
        var all = edges.ToList();
        _edges.Setup(r => r.ListByConsumedLotIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                (IReadOnlyCollection<LotGenealogyEdge>)all.Where(e => ids.Contains(e.ConsumedLotId)).ToList());
    }

    [Fact]
    public void Request_ImplementsTenantRequest()
    {
        typeof(GetDownstreamTraceabilityRequest).Should().Implement<ITenantRequest<LotTraceabilityResponse>>();
    }

    [Fact]
    public async Task Handle_FanOut_ReturnsAllFinishedLots()
    {
        // Arrange: material M consumed by three finished lots.
        var materialId = Guid.NewGuid();
        var f1Id = Guid.NewGuid();
        var f2Id = Guid.NewGuid();
        var f3Id = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var material = NewLot(materialId, "MAT");
        var f1 = NewLot(f1Id, "FIN-1");
        var f2 = NewLot(f2Id, "FIN-2");
        var f3 = NewLot(f3Id, "FIN-3");
        var order = NewOrder(orderId, "PO-F");

        var t = new DateTime(2026, 9, 24, 9, 0, 0, DateTimeKind.Utc);
        var machine = Guid.NewGuid();
        var edge2 = NewEdge(materialId, f2Id, orderId, t.AddMinutes(1));
        edge2.MachineId = machine;
        edge2.ConsumedQuantity = 3m;
        var edges = new[]
        {
            NewEdge(materialId, f1Id, orderId, t),
            edge2,
            NewEdge(materialId, f3Id, orderId, t.AddMinutes(2))
        };

        SetupLotsAndOrders([material, f1, f2, f3], [order]);
        SetupDownstreamEdges(edges);

        // Act
        var result = await CreateSut().Handle(new GetDownstreamTraceabilityRequest(materialId, null), CancellationToken.None);

        // Assert
        result.RootLotId.Should().Be(materialId);
        result.RootLotCode.Should().Be("MAT");
        result.Truncated.Should().BeFalse();
        result.Nodes.Should().HaveCount(3);
        result.Nodes.All(n => n.Depth == 1).Should().BeTrue();
        result.Nodes.Single(n => n.LotId == f2Id).MachineId.Should().Be(machine);
        result.Nodes.Single(n => n.LotId == f2Id).ConsumedQuantity.Should().Be(3m);
        result.Nodes.Single(n => n.LotId == f2Id).ProductionOrderCode.Should().Be("PO-F");
    }

    [Fact]
    public async Task Handle_TransitiveChain_FollowsMultipleLevels()
    {
        // Arrange: M -> A -> B (two downstream levels).
        var mId = Guid.NewGuid();
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var m = NewLot(mId, "M");
        var a = NewLot(aId, "A");
        var b = NewLot(bId, "B");
        var order = NewOrder(orderId, "PO");

        var t = new DateTime(2026, 9, 24, 9, 0, 0, DateTimeKind.Utc);
        SetupLotsAndOrders([m, a, b], [order]);
        SetupDownstreamEdges([
            NewEdge(mId, aId, orderId, t),
            NewEdge(aId, bId, orderId, t.AddHours(1))
        ]);

        // Act
        var result = await CreateSut().Handle(new GetDownstreamTraceabilityRequest(mId, null), CancellationToken.None);

        // Assert
        result.Nodes.Should().HaveCount(2);
        result.Nodes.Single(n => n.LotId == aId).Depth.Should().Be(1);
        result.Nodes.Single(n => n.LotId == bId).Depth.Should().Be(2);
    }

    [Fact]
    public async Task Handle_Cycle_TerminatesAndListsEachLotOnce()
    {
        // Arrange: downstream cycle M -> X -> Y -> X.
        var mId = Guid.NewGuid();
        var xId = Guid.NewGuid();
        var yId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var m = NewLot(mId, "M");
        var x = NewLot(xId, "X");
        var y = NewLot(yId, "Y");
        var order = NewOrder(orderId, "PO");

        var t = new DateTime(2026, 9, 24, 9, 0, 0, DateTimeKind.Utc);
        SetupLotsAndOrders([m, x, y], [order]);
        SetupDownstreamEdges([
            NewEdge(mId, xId, orderId, t),
            NewEdge(xId, yId, orderId, t.AddMinutes(1)),
            NewEdge(yId, xId, orderId, t.AddMinutes(2))
        ]);

        // Act
        var result = await CreateSut().Handle(new GetDownstreamTraceabilityRequest(mId, 10), CancellationToken.None);

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
        var act = () => CreateSut().Handle(new GetDownstreamTraceabilityRequest(id, null), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    [InlineData(11)]
    public async Task Handle_InvalidDepth_ThrowsValidationException(int maxDepth)
    {
        // Act
        var act = () => CreateSut().Handle(
            new GetDownstreamTraceabilityRequest(Guid.NewGuid(), maxDepth), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_DepthCap_Respected()
    {
        // Arrange: linear downstream chain of 4 levels, request depth 1.
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var lots = ids.Select((id, i) => NewLot(id, $"LOT-{i}")).ToArray();
        var order = NewOrder(Guid.NewGuid(), "PO");
        var t = new DateTime(2026, 9, 24, 9, 0, 0, DateTimeKind.Utc);
        var edges = Enumerable.Range(0, 4)
            .Select(i => NewEdge(ids[i], ids[i + 1], order.Id, t.AddMinutes(i)))
            .ToArray();

        SetupLotsAndOrders(lots, [order]);
        SetupDownstreamEdges(edges);

        // Act
        var result = await CreateSut().Handle(new GetDownstreamTraceabilityRequest(ids[0], 1), CancellationToken.None);

        // Assert
        result.Nodes.Should().ContainSingle().Which.LotId.Should().Be(ids[1]);
    }

    [Fact]
    public async Task Handle_OverCap_Returns500NodesWithTruncatedFlag()
    {
        // Arrange: material consumed by 600 finished lots (depth 1 fan-out).
        var materialId = Guid.NewGuid();
        var material = NewLot(materialId, "MAT");
        var order = NewOrder(Guid.NewGuid(), "PO");
        var t = new DateTime(2026, 9, 24, 9, 0, 0, DateTimeKind.Utc);

        var finished = Enumerable.Range(0, 600)
            .Select(i => NewLot(Guid.NewGuid(), $"F-{i:000}"))
            .ToArray();
        var edges = finished
            .Select((f, i) => NewEdge(materialId, f.Id, order.Id, t.AddSeconds(i)))
            .ToArray();

        SetupLotsAndOrders([material, .. finished], [order]);
        SetupDownstreamEdges(edges);

        // Act
        var result = await CreateSut().Handle(new GetDownstreamTraceabilityRequest(materialId, 10), CancellationToken.None);

        // Assert
        result.Nodes.Should().HaveCount(500);
        result.Truncated.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NoEdges_ReturnsEmptyNodesWithoutTruncation()
    {
        // Arrange
        var rootId = Guid.NewGuid();
        var root = NewLot(rootId, "LONELY");
        SetupLotsAndOrders([root], []);
        SetupDownstreamEdges([]);

        // Act
        var result = await CreateSut().Handle(new GetDownstreamTraceabilityRequest(rootId, null), CancellationToken.None);

        // Assert
        result.Nodes.Should().BeEmpty();
        result.Truncated.Should().BeFalse();
    }
}
