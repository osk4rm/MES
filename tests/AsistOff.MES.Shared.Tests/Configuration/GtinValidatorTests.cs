using AsistOff.MES.Configuration.Application.Features.Products.Common;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class GtinValidatorTests
{
    [Theory]
    [InlineData("96385074")] // EAN-8
    [InlineData("036000291452")] // GTIN-12
    [InlineData("5901234123457")] // EAN-13
    [InlineData("4006381333931")] // EAN-13
    [InlineData("10012345678902")] // GTIN-14
    public void IsValid_WithValidGtin_ReturnsTrue(string ean)
    {
        // Act
        var result = GtinValidator.IsValid(ean);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("5901234123458")] // bad check digit
    [InlineData("96385075")] // bad check digit (EAN-8)
    [InlineData("036000291453")] // bad check digit (GTIN-12)
    [InlineData("10012345678903")] // bad check digit (GTIN-14)
    public void IsValid_WithBadCheckDigit_ReturnsFalse(string ean)
    {
        // Act
        var result = GtinValidator.IsValid(ean);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("590123412345A")] // letters
    [InlineData("59012341234 7")] // inner whitespace
    [InlineData("590-123412345")] // separator
    public void IsValid_WithNonDigits_ReturnsFalse(string ean)
    {
        // Act
        var result = GtinValidator.IsValid(ean);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("1234567")] // 7 digits
    [InlineData("123456789")] // 9 digits
    [InlineData("1234567890")] // 10 digits
    [InlineData("12345678901")] // 11 digits
    [InlineData("123456789012345")] // 15 digits
    [InlineData("12345678901234567890")] // 20 digits
    public void IsValid_WithWrongLength_ReturnsFalse(string ean)
    {
        // Act
        var result = GtinValidator.IsValid(ean);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_WithNullOrEmpty_ReturnsTrue(string? ean)
    {
        // Act — empty stays null (no EAN), which is valid.
        var result = GtinValidator.IsValid(ean);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Normalize_WithSurroundingWhitespace_Trims()
    {
        // Act
        var result = GtinValidator.Normalize("  5901234123457\t");

        // Assert
        result.Should().Be("5901234123457");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_WithNullOrEmpty_ReturnsNull(string? ean)
    {
        // Act
        var result = GtinValidator.Normalize(ean);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void IsValid_WithSurroundingWhitespace_AcceptsTrimmedValue()
    {
        // Act
        var result = GtinValidator.IsValid("  5901234123457  ");

        // Assert
        result.Should().BeTrue();
    }
}
