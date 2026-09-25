using AsistOff.MES.Attachments.Application.Features.Upload;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Attachments;

public class AttachmentContentSnifferTests
{
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
    private static readonly byte[] GifMagic = "GIF89a"u8.ToArray();
    private static readonly byte[] PdfMagic = "%PDF-1.7 test"u8.ToArray();
    private static readonly byte[] BmpMagic = [0x42, 0x4D, 0x36, 0x00];
    private static readonly byte[] WebPMagic =
        Combine("RIFF"u8.ToArray(), [0x00, 0x00, 0x00, 0x00], "WEBP"u8.ToArray());

    private static byte[] Combine(params byte[][] parts) => parts.SelectMany(b => b).ToArray();
    private static readonly byte[] TextBytes = "hello, production order notes"u8.ToArray();
    private static readonly byte[] HtmlBytes = "<html><script>alert(1)</script></html>"u8.ToArray();
    private static readonly byte[] ExeBytes = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];

    [Theory]
    [InlineData("image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })]
    public void DetectKind_PngMagic_DetectsPng(string _, byte[] bytes)
    {
        AttachmentContentSniffer.DetectKind(bytes).Should().Be(SniffedContentKind.Png);
    }

    [Fact]
    public void DetectKind_JpegMagic_DetectsJpeg() =>
        AttachmentContentSniffer.DetectKind(JpegMagic).Should().Be(SniffedContentKind.Jpeg);

    [Fact]
    public void DetectKind_GifMagic_DetectsGif() =>
        AttachmentContentSniffer.DetectKind(GifMagic).Should().Be(SniffedContentKind.Gif);

    [Fact]
    public void DetectKind_PdfMagic_DetectsPdf() =>
        AttachmentContentSniffer.DetectKind(PdfMagic).Should().Be(SniffedContentKind.Pdf);

    [Fact]
    public void DetectKind_BmpMagic_DetectsBmp() =>
        AttachmentContentSniffer.DetectKind(BmpMagic).Should().Be(SniffedContentKind.Bmp);

    [Fact]
    public void DetectKind_WebPMagic_DetectsWebP() =>
        AttachmentContentSniffer.DetectKind(WebPMagic).Should().Be(SniffedContentKind.WebP);

    [Fact]
    public void DetectKind_PlainText_DetectsText() =>
        AttachmentContentSniffer.DetectKind(TextBytes).Should().Be(SniffedContentKind.Text);

    [Fact]
    public void DetectKind_HtmlMarkup_DetectsText() =>
        AttachmentContentSniffer.DetectKind(HtmlBytes).Should().Be(SniffedContentKind.Text);

    [Fact]
    public void DetectKind_ExeBytes_DetectsBinary() =>
        AttachmentContentSniffer.DetectKind(ExeBytes).Should().Be(SniffedContentKind.Binary);

    [Theory]
    [InlineData("image/png")]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    [InlineData("text/csv")]
    [InlineData("image/jpeg")]
    [InlineData("image/gif")]
    [InlineData("image/bmp")]
    [InlineData("image/webp")]
    public void EnsureMatches_MatchingKindAndType_DoesNotThrow(string declared)
    {
        // Arrange
        var bytes = declared switch
        {
            "image/png" => PngMagic,
            "image/jpeg" => JpegMagic,
            "image/gif" => GifMagic,
            "image/bmp" => BmpMagic,
            "image/webp" => WebPMagic,
            "application/pdf" => PdfMagic,
            _ => "a,b,c\n1,2,3\n"u8.ToArray()
        };

        // Act
        var act = () => AttachmentContentSniffer.EnsureMatches(declared, bytes);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureMatches_PdfBytesDeclaredAsPng_ThrowsValidationException()
    {
        // Act
        var act = () => AttachmentContentSniffer.EnsureMatches("image/png", PdfMagic);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void EnsureMatches_HtmlBytesDeclaredAsPng_ThrowsValidationException()
    {
        // Act
        var act = () => AttachmentContentSniffer.EnsureMatches("image/png", HtmlBytes);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void EnsureMatches_ExeBytesDeclaredAsText_ThrowsValidationException()
    {
        // Act
        var act = () => AttachmentContentSniffer.EnsureMatches("text/plain", ExeBytes);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void EnsureMatches_PngBytesDeclaredAsPdf_ThrowsValidationException()
    {
        // Act
        var act = () => AttachmentContentSniffer.EnsureMatches("application/pdf", PngMagic);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void EnsureMatches_WebPBytesDeclaredAsPng_ThrowsValidationException()
    {
        // Act
        var act = () => AttachmentContentSniffer.EnsureMatches("image/png", WebPMagic);

        // Assert
        act.Should().Throw<ValidationException>();
    }
}
