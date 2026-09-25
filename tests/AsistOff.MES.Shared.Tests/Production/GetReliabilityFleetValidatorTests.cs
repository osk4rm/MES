using AsistOff.MES.Production.Application.Features.Reliability;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Production;

public class GetReliabilityFleetValidatorTests
{
    private static readonly DateTime From = new(2026, 9, 7, 6, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 9, 7, 14, 0, 0, DateTimeKind.Utc);

    private readonly GetReliabilityFleetValidator _sut = new();

    [Fact]
    public async Task Validate_ValidRequestWithoutDepartment_IsValid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetReliabilityFleetRequest(From, To, null));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_ValidRequestWithDepartment_IsValid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetReliabilityFleetRequest(From, To, Guid.NewGuid()));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_ReversedWindow_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetReliabilityFleetRequest(To, From, null));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_EqualBoundsWindow_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetReliabilityFleetRequest(From, From, null));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WindowOver93Days_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetReliabilityFleetRequest(From, From.AddDays(93).AddMinutes(1), null));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_EmptyDepartmentId_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetReliabilityFleetRequest(From, To, Guid.Empty));

        // Assert
        result.IsValid.Should().BeFalse();
    }
}
