using AsistOff.MES.Production.Application.Features.Schedule;
using AsistOff.MES.Production.Domain.Entities;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Production;

public class GanttLevelingTests
{
    private static readonly DateTime Start = new(2027, 5, 11, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = Start.AddHours(2);

    private static ScheduledOperation Override(
        Guid? orderId = null, Guid? operationId = null,
        DateTime? start = null, DateTime? end = null) => new()
    {
        Id = Guid.NewGuid(),
        ProductionOrderId = orderId ?? Guid.NewGuid(),
        OperationNodeId = operationId ?? Guid.NewGuid(),
        MachineId = Guid.NewGuid(),
        PlannedStart = start ?? Start,
        PlannedEnd = end ?? End,
        CreatedAt = Start
    };

    [Fact]
    public void FindConflicts_OverlappingWindow_IsReported()
    {
        // Arrange
        var blocking = Override(start: Start.AddHours(1), end: End.AddHours(1));

        // Act
        var result = GanttLeveling.FindConflicts([blocking], Start, End, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().ContainSingle().Which.Id.Should().Be(blocking.Id);
    }

    [Fact]
    public void FindConflicts_ContainedAndContainingWindows_AreReported()
    {
        // Arrange
        var inner = Override(start: Start.AddMinutes(30), end: End.AddMinutes(-30));
        var outer = Override(start: Start.AddHours(-1), end: End.AddHours(1));

        // Act
        var result = GanttLeveling.FindConflicts([inner, outer], Start, End, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(-2, 0)] // ends exactly when the move starts
    [InlineData(2, 4)] // starts exactly when the move ends
    public void FindConflicts_TouchingEdges_DoNotConflict(int startOffsetHours, int endOffsetHours)
    {
        // Arrange
        var neighbour = Override(
            start: Start.AddHours(startOffsetHours),
            end: Start.AddHours(endOffsetHours));

        // Act
        var result = GanttLeveling.FindConflicts([neighbour], Start, End, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void FindConflicts_DisjointWindow_IsIgnored()
    {
        // Arrange
        var far = Override(start: Start.AddDays(1), end: End.AddDays(1));

        // Act
        var result = GanttLeveling.FindConflicts([far], Start, End, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void FindConflicts_OwnSegment_IsExcluded()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var own = Override(orderId, operationId);

        // Act
        var result = GanttLeveling.FindConflicts([own], Start, End, orderId, operationId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void FindConflicts_SameOperationOfAnotherOrder_IsReported()
    {
        // Arrange - same operation node pinned for a different order still
        // occupies the lane: only the exact (order, operation) pair is self.
        var operationId = Guid.NewGuid();
        var other = Override(Guid.NewGuid(), operationId);

        // Act
        var result = GanttLeveling.FindConflicts([other], Start, End, Guid.NewGuid(), operationId);

        // Assert
        result.Should().ContainSingle();
    }

    [Fact]
    public void FindConflicts_Results_AreOrderedByStart()
    {
        // Arrange
        var late = Override(start: Start.AddMinutes(90), end: End.AddHours(1));
        var early = Override(start: Start.AddMinutes(10), end: Start.AddMinutes(50));

        // Act
        var result = GanttLeveling.FindConflicts([late, early], Start, End, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Select(o => o.Id).Should().ContainInOrder(early.Id, late.Id);
    }
}
