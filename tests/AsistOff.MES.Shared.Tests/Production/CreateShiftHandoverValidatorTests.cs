using AsistOff.MES.Production.Application.Features.ShiftHandovers.Create;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Production;

public class CreateShiftHandoverValidatorTests
{
    private static readonly DateTime From = new(2026, 9, 21, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 9, 21, 14, 0, 0, DateTimeKind.Utc);

    private readonly CreateShiftHandoverValidator _sut = new();

    [Fact]
    public async Task Validate_ValidRequest_IsValid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new CreateShiftHandoverRequest(Guid.NewGuid(), From, To, "Bearing replaced, watch vibration."));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyMachineId_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new CreateShiftHandoverRequest(Guid.Empty, From, To, "Notes."));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ReversedWindow_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new CreateShiftHandoverRequest(Guid.NewGuid(), To, From, "Notes."));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_EqualBoundsWindow_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new CreateShiftHandoverRequest(Guid.NewGuid(), From, From, "Notes."));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WindowOver24Hours_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new CreateShiftHandoverRequest(Guid.NewGuid(), From, From.AddHours(24).AddSeconds(1), "Notes."));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WindowExactly24Hours_IsValid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new CreateShiftHandoverRequest(Guid.NewGuid(), From, From.AddHours(24), "Notes."));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyNotes_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new CreateShiftHandoverRequest(Guid.NewGuid(), From, To, string.Empty));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WhitespaceNotes_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new CreateShiftHandoverRequest(Guid.NewGuid(), From, To, "   "));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_NotesOver2000Chars_IsInvalid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new CreateShiftHandoverRequest(Guid.NewGuid(), From, To, new string('n', 2001)));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_NotesExactly2000Chars_IsValid()
    {
        // Act
        var result = await _sut.ValidateAsync(
            new CreateShiftHandoverRequest(Guid.NewGuid(), From, To, new string('n', 2000)));

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
