using AsistOff.MES.Configuration.Application.Features.Products.ByScan;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class GetProductByScanRequestValidatorTests
{
    private readonly GetProductByScanRequestValidator _validator = new();

    [Fact]
    public async Task Validate_NullValue_IsInvalid()
    {
        // Arrange
        var request = new GetProductByScanRequest(null);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.Value));
    }

    [Fact]
    public async Task Validate_EmptyValue_IsInvalid()
    {
        // Arrange
        var request = new GetProductByScanRequest(string.Empty);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.Value));
    }

    [Fact]
    public async Task Validate_WhitespaceValue_IsInvalid()
    {
        // Arrange
        var request = new GetProductByScanRequest("   ");

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.Value));
    }

    [Fact]
    public async Task Validate_ExactCode_IsValid()
    {
        // Arrange
        var request = new GetProductByScanRequest("SCAN-CODE-1");

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_ValueWithSurroundingWhitespace_IsValid()
    {
        // Arrange — trimming happens in the handler; the validator accepts the raw scan.
        var request = new GetProductByScanRequest("  TRIMMED-CODE\t");

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
