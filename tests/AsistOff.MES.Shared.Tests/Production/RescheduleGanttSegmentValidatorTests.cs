using AsistOff.MES.Production.Application.Features.Schedule;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Production;

public class RescheduleGanttSegmentValidatorTests
{
    private static readonly DateTime Start = new(2027, 5, 11, 8, 0, 0, DateTimeKind.Utc);

    private static RescheduleGanttSegmentRequest Valid() => new(
        Guid.NewGuid(), Guid.NewGuid(), Start, Start.AddHours(2), Guid.NewGuid(), "7", false, null);

    [Fact]
    public async Task Validate_ValidRequest_IsValid()
    {
        // Arrange
        var validator = new RescheduleGanttSegmentValidator();

        // Act
        var result = await validator.ValidateAsync(Valid());

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_ForceAndNotes_AreAccepted()
    {
        // Arrange
        var validator = new RescheduleGanttSegmentValidator();
        var request = Valid() with { Force = true, Notes = "dragged by planner" };

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("node")]
    [InlineData("order")]
    [InlineData("machine")]
    public async Task Validate_EmptyIds_AreInvalid(string which)
    {
        // Arrange
        var validator = new RescheduleGanttSegmentValidator();
        var valid = Valid();
        var request = which switch
        {
            "node" => valid with { OperationNodeId = Guid.Empty },
            "order" => valid with { ProductionOrderId = Guid.Empty },
            _ => valid with { MachineId = Guid.Empty }
        };

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_MissingDates_AreInvalid()
    {
        // Arrange
        var validator = new RescheduleGanttSegmentValidator();

        // Act
        var missingStart = await validator.ValidateAsync(Valid() with { PlannedStart = default });
        var missingEnd = await validator.ValidateAsync(Valid() with { PlannedEnd = default });

        // Assert
        missingStart.IsValid.Should().BeFalse();
        missingEnd.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_StartAfterEnd_IsInvalid()
    {
        // Arrange
        var validator = new RescheduleGanttSegmentValidator();

        // Act
        var result = await validator.ValidateAsync(Valid() with
        {
            PlannedStart = Start.AddHours(2),
            PlannedEnd = Start
        });

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_Window32Days_IsInvalid_Window31Days_IsValid()
    {
        // Arrange
        var validator = new RescheduleGanttSegmentValidator();

        // Act
        var tooWide = await validator.ValidateAsync(Valid() with { PlannedEnd = Start.AddDays(32) });
        var maxWide = await validator.ValidateAsync(Valid() with { PlannedEnd = Start.AddDays(31) });

        // Assert
        tooWide.IsValid.Should().BeFalse();
        maxWide.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_MissingToken_IsInvalid(string? token)
    {
        // Arrange
        var validator = new RescheduleGanttSegmentValidator();

        // Act
        var result = await validator.ValidateAsync(Valid() with { ConcurrencyToken = token });

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_NotesOver2000Chars_IsInvalid()
    {
        // Arrange
        var validator = new RescheduleGanttSegmentValidator();

        // Act
        var result = await validator.ValidateAsync(Valid() with { Notes = new string('n', 2001) });

        // Assert
        result.IsValid.Should().BeFalse();
    }
}
