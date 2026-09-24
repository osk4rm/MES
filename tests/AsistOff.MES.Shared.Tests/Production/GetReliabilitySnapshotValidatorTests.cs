using AsistOff.MES.Production.Application.Features.Reliability;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Production;

public class GetReliabilitySnapshotValidatorTests
{
    private static readonly DateTime From = new(2026, 9, 7, 6, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 9, 7, 14, 0, 0, DateTimeKind.Utc);

    private readonly GetReliabilitySnapshotValidator _sut = new();

    [Fact]
    public async Task Validate_ValidRequest_IsValid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetReliabilitySnapshotRequest(Guid.NewGuid(), From, To));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyMachineId_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetReliabilitySnapshotRequest(Guid.Empty, From, To));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ReversedWindow_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetReliabilitySnapshotRequest(Guid.NewGuid(), To, From));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_EqualBoundsWindow_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetReliabilitySnapshotRequest(Guid.NewGuid(), From, From));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WindowOver93Days_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetReliabilitySnapshotRequest(Guid.NewGuid(), From, From.AddDays(93).AddMinutes(1)));

        // Assert
        result.IsValid.Should().BeFalse();
    }
}
