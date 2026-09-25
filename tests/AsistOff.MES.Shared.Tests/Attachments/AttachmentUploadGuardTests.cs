using AsistOff.MES.Attachments.Application.Features.Upload;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Attachments;

public class AttachmentUploadGuardTests
{
    private static AttachmentUploadOptions Defaults() => new();

    [Theory]
    [InlineData("photo.png", "image/png")]
    [InlineData("photo.JPG", "image/jpeg")]
    [InlineData("photo.jpeg", "image/jpg")]
    [InlineData("anim.gif", "image/gif")]
    [InlineData("img.webp", "image/webp")]
    [InlineData("scan.bmp", "image/bmp")]
    [InlineData("doc.pdf", "application/pdf")]
    [InlineData("notes.txt", "text/plain")]
    [InlineData("data.csv", "text/csv")]
    [InlineData("data.csv", "text/csv; charset=utf-8")]
    [InlineData("NOTES.TXT", "TEXT/PLAIN")]
    public void EnsureAllowed_AllowlistedExtensionAndType_ReturnsNormalized(string fileName, string contentType)
    {
        // Act
        var normalized = AttachmentUploadGuard.EnsureAllowed(fileName, contentType, Defaults());

        // Assert
        normalized.Should().NotBeNullOrWhiteSpace();
        normalized.Should().NotContain(";");
    }

    [Fact]
    public void EnsureAllowed_JpgAlias_NormalizesToJpeg()
    {
        // Act
        var normalized = AttachmentUploadGuard.EnsureAllowed("photo.jpeg", "image/jpg", Defaults());

        // Assert
        normalized.Should().Be("image/jpeg");
    }

    [Theory]
    [InlineData("evil.html", "text/html")]
    [InlineData("app.js", "application/javascript")]
    [InlineData("run.exe", "application/x-msdownload")]
    [InlineData("payload.svg", "image/svg+xml")]
    [InlineData("noextension", "text/plain")]
    [InlineData("notes.txt", "text/html")]
    [InlineData("photo.png", "")]
    [InlineData("photo.png", null)]
    public void EnsureAllowed_DisallowedExtensionOrType_ThrowsValidationException(string fileName, string? contentType)
    {
        // Act
        var act = () => AttachmentUploadGuard.EnsureAllowed(fileName, contentType, Defaults());

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Theory]
    [InlineData("image/png", "image/png")]
    [InlineData("text/plain", "text/plain")]
    [InlineData("text/html", "application/octet-stream")]
    [InlineData("application/x-msdownload", "application/octet-stream")]
    [InlineData("", "application/octet-stream")]
    [InlineData(null, "application/octet-stream")]
    public void MapDownloadContentType_MapsSafely(string? stored, string expected)
    {
        // Act
        var mapped = AttachmentUploadGuard.MapDownloadContentType(stored, Defaults());

        // Assert
        mapped.Should().Be(expected);
    }
}
