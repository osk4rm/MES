using AsistOff.MES.Production.Application.Features.Reliability.Trend;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Production;

public class GetReliabilityTrendValidatorTests
{
    private static readonly DateTime From = new(2026, 9, 7, 6, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 9, 7, 14, 0, 0, DateTimeKind.Utc);

    private readonly GetReliabilityTrendValidator _sut = new();

    [Fact]
    public async Task Validate_ValidDayRequest_IsValid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetReliabilityTrendRequest(Guid.NewGuid(), From, To, "Day"));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_ValidWeekRequest_IsValid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetReliabilityTrendRequest(Guid.NewGuid(), From, To.AddDays(13), "Week"));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("day")]
    [InlineData("WEEK")]
    public async Task Validate_BucketNames_AreCaseInsensitive(string bucket)
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetReliabilityTrendRequest(Guid.NewGuid(), From, To, bucket));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyMachineId_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetReliabilityTrendRequest(Guid.Empty, From, To, "Day"));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ReversedWindow_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetReliabilityTrendRequest(Guid.NewGuid(), To, From, "Day"));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_EqualBoundsWindow_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetReliabilityTrendRequest(Guid.NewGuid(), From, From, "Day"));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WindowOver93Days_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetReliabilityTrendRequest(Guid.NewGuid(), From, From.AddDays(93).AddMinutes(1), "Day"));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("Month")]
    [InlineData("daily")]
    [InlineData("99")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public async Task Validate_UnknownBucket_IsInvalid(string? bucket)
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetReliabilityTrendRequest(Guid.NewGuid(), From, To, bucket));

        // Assert
        result.IsValid.Should().BeFalse();
    }
}
