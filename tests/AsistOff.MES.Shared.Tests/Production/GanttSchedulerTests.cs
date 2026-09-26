using AsistOff.MES.Production.Application.Features.Schedule;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Production;

public class GanttSchedulerTests
{
    private static readonly DateTime AnchorNow = new(2026, 9, 21, 8, 0, 0, DateTimeKind.Utc);

    private static ProductionOrder MakeOrder(decimal planned = 100m, DateTime? dueDate = null) => new()
    {
        Id = Guid.NewGuid(),
        Code = "PO-001",
        ProductId = Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        RecipeVersionId = Guid.NewGuid(),
        PlannedQuantity = planned,
        Status = ProductionOrderStatus.Released,
        DueDate = dueDate,
        CreatedAt = AnchorNow
    };

    private static OperationNode MakeNode(
        string code,
        int sortIndex = 0,
        decimal? setup = null,
        RunTimeMode mode = RunTimeMode.PerUnitSeconds,
        decimal? perUnitSeconds = null,
        decimal? perBatchMinutes = null,
        decimal? teardown = null,
        decimal? queue = null,
        Guid? machineId = null) => new()
    {
        Id = Guid.NewGuid(),
        RecipeVersionId = Guid.NewGuid(),
        Code = code,
        Name = code,
        SortIndex = sortIndex,
        SetupTimeMinutes = setup,
        RunTimeMode = mode,
        RunTimePerUnitSeconds = perUnitSeconds,
        RunTimePerBatchMinutes = perBatchMinutes,
        TeardownTimeMinutes = teardown,
        QueueTimeMinutes = queue,
        ResourceRequirements = machineId.HasValue
            ? [new ResourceRequirement { Id = Guid.NewGuid(), OperationNodeId = Guid.Empty, PreferredMachineId = machineId }]
            : []
    };

    private static OperationDependency MakeEdge(Guid successor, Guid predecessor, OperationDependencyType type, decimal? lag = null) => new()
    {
        Id = Guid.NewGuid(),
        RecipeVersionId = Guid.NewGuid(),
        OperationNodeId = successor,
        PredecessorOperationNodeId = predecessor,
        DependencyType = type,
        LagMinutes = lag
    };

    [Fact]
    public void ComputeDuration_PerUnitSeconds_ScalesWithPlannedQuantity()
    {
        // Arrange - 10s/unit x 100 units = 1000s = 16.666...min plus 5min setup
        var node = MakeNode("OP-10", setup: 5m, perUnitSeconds: 10m);

        // Act
        var duration = GanttScheduler.ComputeDurationMinutes(node, 100m);

        // Assert
        duration.Should().BeApproximately(5m + 100m * 10m / 60m, 0.0001m);
    }

    [Fact]
    public void ComputeDuration_PerBatchMinutes_IsFlatRegardlessOfQuantity()
    {
        // Arrange
        var node = MakeNode("OP-20", mode: RunTimeMode.PerBatchMinutes, perBatchMinutes: 45m, teardown: 5m);

        // Act
        var small = GanttScheduler.ComputeDurationMinutes(node, 1m);
        var large = GanttScheduler.ComputeDurationMinutes(node, 10000m);

        // Assert
        small.Should().Be(50m);
        large.Should().Be(50m);
    }

    [Fact]
    public void ComputeDuration_AllComponents_SumsSetupRunTeardownQueue()
    {
        // Arrange - 60s/unit x 60 units = 60min run + 10 setup + 5 teardown + 15 queue
        var node = MakeNode("OP-30", setup: 10m, perUnitSeconds: 60m, teardown: 5m, queue: 15m);

        // Act
        var duration = GanttScheduler.ComputeDurationMinutes(node, 60m);

        // Assert
        duration.Should().Be(90m);
    }

    [Fact]
    public void ComputeOrderSegments_FinishToStartWithLag_ChainsWithGap()
    {
        // Arrange - A (60min) -> B (30min) with 15min lag, forward-anchored
        var order = MakeOrder(60m);
        var a = MakeNode("OP-A", sortIndex: 0, perUnitSeconds: 60m);
        var b = MakeNode("OP-B", sortIndex: 1, perUnitSeconds: 30m);
        b.Dependencies.Add(MakeEdge(b.Id, a.Id, OperationDependencyType.FinishToStart, 15m));

        // Act
        var segments = GanttScheduler.ComputeOrderSegments(order, [a, b], AnchorNow);

        // Assert
        segments.Should().HaveCount(2);
        var first = segments.Single(s => s.OperationCode == "OP-A");
        var second = segments.Single(s => s.OperationCode == "OP-B");
        first.PlannedStart.Should().Be(AnchorNow);
        first.PlannedEnd.Should().Be(AnchorNow.AddMinutes(60));
        second.PlannedStart.Should().Be(AnchorNow.AddMinutes(75));
        second.PlannedEnd.Should().Be(AnchorNow.AddMinutes(105));
    }

    [Fact]
    public void ComputeOrderSegments_NonFinishToStartEdge_FallsBackToSequential()
    {
        // Arrange - StartToStart edge with a 120min lag claim: lag is ignored,
        // the successor simply starts when the predecessor ends.
        var order = MakeOrder(60m);
        var a = MakeNode("OP-A", sortIndex: 0, perUnitSeconds: 60m);
        var b = MakeNode("OP-B", sortIndex: 1, perUnitSeconds: 30m);
        b.Dependencies.Add(MakeEdge(b.Id, a.Id, OperationDependencyType.StartToStart, 120m));

        // Act
        var segments = GanttScheduler.ComputeOrderSegments(order, [a, b], AnchorNow);

        // Assert
        var second = segments.Single(s => s.OperationCode == "OP-B");
        second.PlannedStart.Should().Be(AnchorNow.AddMinutes(60));
        second.PlannedEnd.Should().Be(AnchorNow.AddMinutes(90));
    }

    [Fact]
    public void ComputeOrderSegments_NoDueDate_AnchorsForwardFromNow()
    {
        // Arrange
        var order = MakeOrder(60m, dueDate: null);
        var a = MakeNode("OP-A", perUnitSeconds: 60m);

        // Act
        var segments = GanttScheduler.ComputeOrderSegments(order, [a], AnchorNow);

        // Assert
        var segment = segments.Should().ContainSingle().Subject;
        segment.PlannedStart.Should().Be(AnchorNow);
        segment.PlannedEnd.Should().Be(AnchorNow.AddMinutes(60));
    }

    [Fact]
    public void ComputeOrderSegments_WithDueDate_AnchorsBackwardSoChainEndsAtDueDate()
    {
        // Arrange - 60min + 30min chain due at noon: starts at 10:30, ends at noon
        var dueDate = new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);
        var order = MakeOrder(60m, dueDate: dueDate);
        var a = MakeNode("OP-A", sortIndex: 0, perUnitSeconds: 60m);
        var b = MakeNode("OP-B", sortIndex: 1, perUnitSeconds: 30m);
        b.Dependencies.Add(MakeEdge(b.Id, a.Id, OperationDependencyType.FinishToStart));

        // Act
        var segments = GanttScheduler.ComputeOrderSegments(order, [a, b], AnchorNow);

        // Assert
        segments.Should().HaveCount(2);
        segments.Max(s => s.PlannedEnd).Should().Be(dueDate);
        segments.Min(s => s.PlannedStart).Should().Be(dueDate.AddMinutes(-90));
        var second = segments.Single(s => s.OperationCode == "OP-B");
        second.PlannedStart.Should().Be(dueDate.AddMinutes(-30));
    }

    [Fact]
    public void ComputeOrderSegments_PreferredMachine_ResolvesFromResourceRequirement()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        var order = MakeOrder(10m);
        var withMachine = MakeNode("OP-M", perUnitSeconds: 60m, machineId: machineId);
        var withoutMachine = MakeNode("OP-U", sortIndex: 1, perUnitSeconds: 60m);

        // Act
        var segments = GanttScheduler.ComputeOrderSegments(order, [withMachine, withoutMachine], AnchorNow);

        // Assert
        segments.Single(s => s.OperationCode == "OP-M").MachineId.Should().Be(machineId);
        segments.Single(s => s.OperationCode == "OP-U").MachineId.Should().BeNull();
    }

    [Fact]
    public void ComputeOrderSegments_NoNodes_ReturnsEmpty()
    {
        // Act
        var segments = GanttScheduler.ComputeOrderSegments(MakeOrder(), [], AnchorNow);

        // Assert
        segments.Should().BeEmpty();
    }
}
