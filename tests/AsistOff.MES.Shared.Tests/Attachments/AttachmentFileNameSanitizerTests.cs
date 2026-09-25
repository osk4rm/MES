using AsistOff.MES.Attachments.Application.Features.Common;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Attachments;

public class AttachmentFileNameSanitizerTests
{
    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("..\\..\\secret.txt", "secret.txt")]
    [InlineData("C:\\temp\\evil.png", "evil.png")]
    [InlineData("/abs/path/report.pdf", "report.pdf")]
    public void Sanitize_PathSeparators_StripsDirectories(string input, string expected)
    {
        AttachmentFileNameSanitizer.Sanitize(input).Should().Be(expected);
    }

    [Fact]
    public void Sanitize_NonAscii_ReplacesWithUnderscores()
    {
        // Act
        var result = AttachmentFileNameSanitizer.Sanitize("zażółć.pdf");

        // Assert
        result.Should().MatchRegex("^[A-Za-z0-9._-]+$");
        result.Should().EndWith(".pdf");
    }

    [Fact]
    public void Sanitize_HeaderInjectionChars_RemovesThem()
    {
        // Act
        var result = AttachmentFileNameSanitizer.Sanitize("evil\r\nContent-Type: x.png\";.pdf");

        // Assert
        result.Should().NotContain("\r");
        result.Should().NotContain("\n");
        result.Should().NotContain("\"");
        result.Should().NotContain(";");
        result.Should().NotContain(":");
        result.Should().NotContain(" ");
        result.Should().MatchRegex("^[A-Za-z0-9._-]+$");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    public void Sanitize_EmptyOrDotSegments_FallsBackToFile(string? input)
    {
        AttachmentFileNameSanitizer.Sanitize(input).Should().Be("file");
    }

    [Fact]
    public void Sanitize_PlainName_PreservesIt()
    {
        AttachmentFileNameSanitizer.Sanitize("report-2024_final.PDF").Should().Be("report-2024_final.PDF");
    }

    [Fact]
    public void Sanitize_LongName_TruncatesToLimit()
    {
        // Arrange
        var input = new string('a', 300) + ".pdf";

        // Act
        var result = AttachmentFileNameSanitizer.Sanitize(input);

        // Assert
        result.Length.Should().BeLessThanOrEqualTo(AttachmentFileNameSanitizer.MaxLength);
    }
}
