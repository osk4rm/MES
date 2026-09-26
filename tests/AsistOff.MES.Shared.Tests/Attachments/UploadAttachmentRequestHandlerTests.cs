using AsistOff.MES.Attachments.Application.Features.Common;
using AsistOff.MES.Attachments.Application.Features.Upload;
using AsistOff.MES.Attachments.Domain.Entities;
using AsistOff.MES.Attachments.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Shared.Abstractions.Storage;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace AsistOff.MES.Shared.Tests.Attachments;

public class UploadAttachmentRequestHandlerTests
{
    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03];

    private static readonly byte[] PdfBytes = "%PDF-1.7 payload"u8.ToArray();

    private readonly Mock<IAttachmentsRepository> _repository = new();
    private readonly Mock<IFileStorage> _storage = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<IAttachmentOwnerVerifier> _owners = new();
    private readonly AttachmentUploadOptions _options = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _attachmentId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();

    public UploadAttachmentRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(_attachmentId);
        _clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
        _owners.Setup(o => o.ExistsAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _storage.Setup(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ab/cd/key-file.png");
        _repository.Setup(r => r.AddAsync(It.IsAny<Attachment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Attachment a, CancellationToken _) => a);
    }

    private UploadAttachmentRequestHandler CreateSut() =>
        new(_repository.Object, _storage.Object, _guids.Object, _clock.Object,
            _tenant.Object, Options.Create(_options), _owners.Object);

    private static UploadAttachmentRequest Request(
        string fileName, string contentType, byte[] bytes, Guid? ownerId = null, string ownerType = "operation") =>
        new(ownerType, ownerId ?? Guid.NewGuid(), fileName, contentType, bytes.Length, new MemoryStream(bytes), null);

    [Fact]
    public async Task Handle_ValidPng_PersistsSanitizedMetadata()
    {
        // Arrange
        Attachment? persisted = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<Attachment>(), It.IsAny<CancellationToken>()))
            .Callback<Attachment, CancellationToken>((a, _) => persisted = a)
            .ReturnsAsync((Attachment a, CancellationToken _) => a);

        // Act
        var result = await CreateSut().Handle(Request("photo.png", "image/png", PngBytes, _ownerId), CancellationToken.None);

        // Assert
        result.FileName.Should().Be("photo.png");
        result.ContentType.Should().Be("image/png");
        result.SizeBytes.Should().Be(PngBytes.Length);
        persisted.Should().NotBeNull();
        persisted!.TenantId.Should().Be(_tenantId);
        persisted.OwnerId.Should().Be(_ownerId);
        persisted.StorageKey.Should().Be("ab/cd/key-file.png");
        _storage.Verify(s => s.SaveAsync(It.IsAny<Stream>(), "image/png", "photo.png", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DisallowedExtension_ThrowsValidationExceptionAndPersistsNothing()
    {
        // Act
        var act = () => CreateSut().Handle(Request("evil.html", "text/html", "<html></html>"u8.ToArray()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _storage.Verify(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.AddAsync(It.IsAny<Attachment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DisallowedMimeType_ThrowsValidationExceptionAndPersistsNothing()
    {
        // Act
        var act = () => CreateSut().Handle(Request("run.exe", "application/x-msdownload", ExeBytes()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _storage.Verify(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.AddAsync(It.IsAny<Attachment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SniffedBytesMismatchDeclaredType_ThrowsValidationExceptionAndPersistsNothing()
    {
        // Arrange - PDF bytes declared as PNG
        var request = Request("fake.png", "image/png", PdfBytes);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _storage.Verify(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.AddAsync(It.IsAny<Attachment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SizeAboveMaximum_ThrowsValidationExceptionBeforePersistence()
    {
        // Arrange
        var request = new UploadAttachmentRequest(
            "operation", _ownerId, "big.png", "image/png",
            _options.MaxFileSizeBytes + 1, new MemoryStream(PngBytes), null);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _storage.Verify(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.AddAsync(It.IsAny<Attachment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_StreamLongerThanMaximum_ThrowsValidationExceptionBeforePersistence()
    {
        // Arrange - small configured max, stream exceeds it even though SizeBytes looks fine
        _options.MaxFileSizeBytes = 8;
        var request = Request("photo.png", "image/png", PngBytes);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _storage.Verify(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.AddAsync(It.IsAny<Attachment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownOwner_ThrowsNotFoundExceptionAndPersistsNothing()
    {
        // Arrange
        _owners.Setup(o => o.ExistsAsync("operation", _ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var request = Request("photo.png", "image/png", PngBytes, _ownerId);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _storage.Verify(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.AddAsync(It.IsAny<Attachment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NullContent_ThrowsValidationExceptionAndPersistsNothing()
    {
        // Arrange - a null stream must fail closed with 400, never NRE into a 500
        var request = new UploadAttachmentRequest(
            "operation", _ownerId, "photo.png", "image/png", PngBytes.Length, null!, null);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _storage.Verify(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.AddAsync(It.IsAny<Attachment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static byte[] ExeBytes() => [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];
}
