using AsistOff.MES.Production.Application.Features.ShiftHandovers;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Production;

public class GetShiftHandoverContextValidatorTests
{
    private static readonly DateTime From = new(2026, 9, 21, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 9, 21, 14, 0, 0, DateTimeKind.Utc);

    private readonly GetShiftHandoverContextValidator _sut = new();

    [Fact]
    public async Task Validate_ValidRequest_IsValid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetShiftHandoverContextRequest(Guid.NewGuid(), From, To));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_NullMachineId_IsValid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetShiftHandoverContextRequest(null, From, To));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyMachineId_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetShiftHandoverContextRequest(Guid.Empty, From, To));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ReversedWindow_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetShiftHandoverContextRequest(null, To, From));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_EqualBoundsWindow_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetShiftHandoverContextRequest(null, From, From));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WindowOver24Hours_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetShiftHandoverContextRequest(null, From, From.AddHours(24).AddSeconds(1)));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WindowExactly24Hours_IsValid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetShiftHandoverContextRequest(null, From, From.AddHours(24)));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_MissingFrom_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetShiftHandoverContextRequest(null, default, To));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_MissingTo_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetShiftHandoverContextRequest(null, From, default));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ConfirmationPageZero_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetShiftHandoverContextRequest(null, From, To, 0, 20));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ConfirmationPageSizeZero_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetShiftHandoverContextRequest(null, From, To, 1, 0));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ConfirmationPageSizeOver100_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetShiftHandoverContextRequest(null, From, To, 1, 101));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ConfirmationPageSize100_IsValid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new GetShiftHandoverContextRequest(null, From, To, 1, 100));

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
