using AsistOff.MES.Attachments.Application.Features.Download;
using AsistOff.MES.Attachments.Domain.Entities;
using AsistOff.MES.Attachments.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Storage;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Attachments;

public class DownloadAttachmentRequestHandlerTests
{
    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03];

    private readonly Mock<IAttachmentsRepository> _repository = new();
    private readonly Mock<IFileStorage> _storage = new();
    private readonly Guid _attachmentId = Guid.NewGuid();

    private DownloadAttachmentRequestHandler CreateSut() => new(_repository.Object, _storage.Object);

    [Fact]
    public async Task Handle_KnownId_ReturnsStoredContentWithMetadata()
    {
        // Arrange - PNG variant (branch)
        var entity = new Attachment
        {
            Id = _attachmentId,
            TenantId = Guid.NewGuid(),
            OwnerType = "operation",
            OwnerId = Guid.NewGuid(),
            FileName = "photo.png",
            ContentType = "image/png",
            SizeBytes = PngBytes.Length,
            StorageKey = "ab/cd/key-file.png",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        _repository.Setup(r => r.GetAsync(_attachmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        _storage.Setup(s => s.OpenReadAsync("ab/cd/key-file.png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(PngBytes));

        // Act
        var result = await CreateSut().Handle(new DownloadAttachmentRequest(_attachmentId), CancellationToken.None);

        // Assert
        result.FileName.Should().Be("photo.png");
        result.ContentType.Should().Be("image/png");
        result.SizeBytes.Should().Be(PngBytes.Length);
        await using var content = result.Content;
        using var buffered = new MemoryStream();
        await content.CopyToAsync(buffered);
        buffered.ToArray().Should().Equal(PngBytes);
        _storage.Verify(s => s.OpenReadAsync("ab/cd/key-file.png", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_KnownId_ReturnsContentWithMetadata()
    {
        // Arrange - text variant (master)
        var bytes = "hello, production notes"u8.ToArray();
        var entity = new Attachment
        {
            Id = _attachmentId,
            TenantId = Guid.NewGuid(),
            OwnerType = "operation",
            OwnerId = Guid.NewGuid(),
            FileName = "notes.txt",
            ContentType = "text/plain",
            SizeBytes = bytes.Length,
            StorageKey = "ab/cd/key-notes.txt",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        _repository.Setup(r => r.GetAsync(_attachmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        _storage.Setup(s => s.OpenReadAsync("ab/cd/key-notes.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(bytes));

        // Act
        var result = await CreateSut().Handle(new DownloadAttachmentRequest(_attachmentId), CancellationToken.None);

        // Assert
        result.FileName.Should().Be("notes.txt");
        result.ContentType.Should().Be("text/plain");
        result.SizeBytes.Should().Be(bytes.Length);
        using (result.Content)
        {
            using var reader = new MemoryStream();
            await result.Content.CopyToAsync(reader);
            reader.ToArray().Should().Equal(bytes);
        }
        _storage.Verify(s => s.OpenReadAsync("ab/cd/key-notes.txt", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFoundExceptionWithoutOpeningStorage()
    {
        // Arrange - repository is tenant-filtered, so unknown ids and
        // other-tenant ids both surface as null here
        _repository.Setup(r => r.GetAsync(_attachmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Attachment?)null);

        // Act
        var act = () => CreateSut().Handle(new DownloadAttachmentRequest(_attachmentId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _storage.Verify(s => s.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
