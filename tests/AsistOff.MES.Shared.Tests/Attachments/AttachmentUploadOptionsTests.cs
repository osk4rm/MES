using AsistOff.MES.Attachments.Application.Features.Upload;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Attachments;

public class AttachmentUploadOptionsTests
{
    [Fact]
    public void Normalize_Defaults_RestoresCaseInsensitiveMatching()
    {
        // Arrange - simulates what configuration binding does: fresh sets
        // with the default (case-sensitive) comparer and raw values.
        var options = new AttachmentUploadOptions
        {
            AllowedExtensions = new HashSet<string> { ".png", ".PDF" },
            AllowedContentTypes = new HashSet<string> { "image/png", "Application/PDF" }
        };

        // Act
        options.Normalize();

        // Assert
        options.AllowedExtensions.Should().Contain(".PNG");
        options.AllowedExtensions.Should().Contain(".pdf");
        options.AllowedContentTypes.Should().Contain("IMAGE/PNG");
        options.AllowedContentTypes.Should().Contain("application/pdf");
    }

    [Fact]
    public void Normalize_MimeParametersAndJpgAlias_AreCanonicalized()
    {
        // Arrange
        var options = new AttachmentUploadOptions
        {
            AllowedContentTypes = new HashSet<string> { "text/csv; charset=utf-8", "image/jpg", "  TEXT/PLAIN  " }
        };

        // Act
        options.Normalize();

        // Assert
        options.AllowedContentTypes.Should().BeEquivalentTo("text/csv", "image/jpeg", "text/plain");
    }

    [Fact]
    public void Normalize_ExtensionWithoutDot_GetsDottedAndBlankEntriesDropped()
    {
        // Arrange
        var options = new AttachmentUploadOptions
        {
            AllowedExtensions = new HashSet<string> { "png", "  ", ".pdf" }
        };

        // Act
        options.Normalize();

        // Assert
        options.AllowedExtensions.Should().BeEquivalentTo(".png", ".pdf");
    }

    [Fact]
    public void Normalize_NullLists_FallBackToSafeDefaults()
    {
        // Arrange
        var options = new AttachmentUploadOptions
        {
            AllowedExtensions = null!,
            AllowedContentTypes = null!
        };

        // Act
        options.Normalize();

        // Assert
        options.AllowedExtensions.Should().Contain(".png");
        options.AllowedContentTypes.Should().Contain("image/png");
        options.AllowedContentTypes.Should().NotContain("text/html");
    }

    [Fact]
    public void Normalize_ExplicitlyEmptiedLists_StayEmptyDenyAll()
    {
        // Arrange - an operator choosing deny-all must keep working.
        var options = new AttachmentUploadOptions
        {
            AllowedExtensions = new HashSet<string>(),
            AllowedContentTypes = new HashSet<string>()
        };

        // Act
        options.Normalize();

        // Assert
        options.AllowedExtensions.Should().BeEmpty();
        options.AllowedContentTypes.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Normalize_NonPositiveMaxSize_FallsBackToDefault(long maxSize)
    {
        // Arrange
        var options = new AttachmentUploadOptions { MaxFileSizeBytes = maxSize };

        // Act
        options.Normalize();

        // Assert
        options.MaxFileSizeBytes.Should().Be(AttachmentUploadOptions.DefaultMaxFileSizeBytes);
    }

    [Fact]
    public void Normalize_NormalizedOptions_StillRejectDangerousTypes()
    {
        // Arrange
        var options = new AttachmentUploadOptions
        {
            AllowedExtensions = new HashSet<string> { ".png", ".PDF", "txt" },
            AllowedContentTypes = new HashSet<string> { "image/png", "Application/PDF", "text/plain; charset=utf-8" }
        };
        options.Normalize();

        // Act
        var act = () => AttachmentUploadGuard.EnsureAllowed("evil.html", "text/html", options);

        // Assert
        act.Should().Throw<AsistOff.MES.Shared.Abstractions.Exceptions.ValidationException>();
        AttachmentUploadGuard.EnsureAllowed("PHOTO.PNG", "IMAGE/PNG", options).Should().Be("image/png");
    }
}
