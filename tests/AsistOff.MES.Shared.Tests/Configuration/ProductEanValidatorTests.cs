using AsistOff.MES.Configuration.Application.Features.Products.Create;
using AsistOff.MES.Configuration.Application.Features.Products.Update;
using AsistOff.MES.Configuration.Domain.Enums;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class ProductEanValidatorTests
{
    private readonly CreateProductRequestValidator _createValidator = new();
    private readonly UpdateProductRequestValidator _updateValidator = new();

    private static CreateProductRequest CreateRequest(string? ean) =>
        new("EAN-CODE-1", "EAN product", null, ean, null, ScanBy.Code, true, null, null);

    private static UpdateProductRequest UpdateRequest(string? ean) =>
        new(Guid.NewGuid(), "EAN-CODE-1", "EAN product", null, ean, null, ScanBy.Code, true, null, null);

    [Theory]
    [InlineData("5901234123457")]
    [InlineData("96385074")]
    [InlineData("036000291452")]
    [InlineData("10012345678902")]
    public async Task ValidateCreate_WithValidEan_IsValid(string ean)
    {
        // Act
        var result = await _createValidator.ValidateAsync(CreateRequest(ean));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateCreate_WithNullOrEmptyEan_IsValid(string? ean)
    {
        // Act — empty stays null (no EAN).
        var result = await _createValidator.ValidateAsync(CreateRequest(ean));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("5901234123458")] // bad check digit
    [InlineData("590123412345A")] // letters
    [InlineData("123456789")] // wrong length
    [InlineData("1234567")] // too short
    public async Task ValidateCreate_WithInvalidEan_IsInvalid(string ean)
    {
        // Act
        var result = await _createValidator.ValidateAsync(CreateRequest(ean));

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProductRequest.Ean));
    }

    [Fact]
    public async Task ValidateCreate_WithWhitespacePaddedValidEan_IsValid()
    {
        // Act — trimming happens on normalize; the validator accepts the padded scan.
        var result = await _createValidator.ValidateAsync(CreateRequest("  5901234123457  "));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("5901234123457")]
    [InlineData(null)]
    [InlineData("")]
    public async Task ValidateUpdate_WithValidOrEmptyEan_IsValid(string? ean)
    {
        // Act
        var result = await _updateValidator.ValidateAsync(UpdateRequest(ean));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("5901234123458")] // bad check digit
    [InlineData("NOT-AN-EAN")] // letters
    [InlineData("123456789")] // wrong length
    public async Task ValidateUpdate_WithInvalidEan_IsInvalid(string ean)
    {
        // Act
        var result = await _updateValidator.ValidateAsync(UpdateRequest(ean));

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateProductRequest.Ean));
    }
}
