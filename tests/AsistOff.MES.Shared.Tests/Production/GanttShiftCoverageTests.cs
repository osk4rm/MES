using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Production.Application.Features.Schedule;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Production;

public class GanttShiftCoverageTests
{
    private static readonly DateOnly Day = new(2027, 5, 11);

    private static Shift ActiveShift() => new()
    {
        Id = Guid.NewGuid(),
        Code = "S1",
        Name = "Shift 1",
        StartTime = new TimeOnly(6, 0),
        EndTime = new TimeOnly(14, 0),
        IsActive = true
    };

    private static OperatorShiftAssignment Roster(Guid shiftId, DateOnly date, Guid? operatorId = null) => new()
    {
        Id = Guid.NewGuid(),
        OperatorId = operatorId ?? Guid.NewGuid(),
        ShiftId = shiftId,
        Date = date
    };

    [Fact]
    public void HasCoverageWarning_NoActiveShifts_ReturnsTrue()
    {
        // Act
        var result = GanttShiftCoverage.HasCoverageWarning([], [], Day, Day);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasCoverageWarning_ShiftsWithoutRoster_ReturnsTrue()
    {
        // Arrange
        var shift = ActiveShift();

        // Act
        var result = GanttShiftCoverage.HasCoverageWarning([shift], [], Day, Day);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasCoverageWarning_RosterOutsideSpan_ReturnsTrue()
    {
        // Arrange - the roster row exists but on another date.
        var shift = ActiveShift();

        // Act
        var result = GanttShiftCoverage.HasCoverageWarning(
            [shift], [Roster(shift.Id, Day.AddDays(5))], Day, Day);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasCoverageWarning_RosteredDate_ReturnsFalse()
    {
        // Arrange
        var shift = ActiveShift();

        // Act
        var result = GanttShiftCoverage.HasCoverageWarning(
            [shift], [Roster(shift.Id, Day)], Day, Day);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasCoverageWarning_RosterOnAnySpannedDate_CoversWholeWindow()
    {
        // Arrange
        var shift = ActiveShift();

        // Act
        var result = GanttShiftCoverage.HasCoverageWarning(
            [shift], [Roster(shift.Id, Day.AddDays(1))], Day, Day.AddDays(2));

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasCoverageWarning_DuplicateRows_CountOneHeadcount()
    {
        // Arrange - the same operator rostered twice on one shift is still cover.
        var shift = ActiveShift();
        var operatorId = Guid.NewGuid();
        var roster = new[]
        {
            Roster(shift.Id, Day, operatorId),
            Roster(shift.Id, Day, operatorId)
        };

        // Act
        var result = GanttShiftCoverage.HasCoverageWarning([shift], roster, Day, Day);

        // Assert
        result.Should().BeFalse();
    }
}
