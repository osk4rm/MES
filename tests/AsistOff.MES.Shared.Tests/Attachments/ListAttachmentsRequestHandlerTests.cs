using AsistOff.MES.Attachments.Application.Features.Common;
using AsistOff.MES.Attachments.Application.Features.List;
using AsistOff.MES.Attachments.Domain.Entities;
using AsistOff.MES.Attachments.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Attachments;

public class ListAttachmentsRequestHandlerTests
{
    private readonly Mock<IAttachmentsRepository> _repository = new();
    private readonly Mock<IAttachmentOwnerVerifier> _owners = new();
    private readonly Guid _ownerId = Guid.NewGuid();

    public ListAttachmentsRequestHandlerTests()
    {
        _owners.Setup(o => o.ExistsAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private ListAttachmentsRequestHandler CreateSut() => new(_repository.Object, _owners.Object);

    [Fact]
    public async Task Handle_UnknownOwner_ThrowsNotFoundExceptionWithoutQuerying()
    {
        // Arrange
        _owners.Setup(o => o.ExistsAsync("operation", _ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var act = () => CreateSut().Handle(new ListAttachmentsRequest("operation", _ownerId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _repository.Verify(r => r.ListForOwnerAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_KnownOwner_ReturnsMappedAttachments()
    {
        // Arrange
        var entity = new Attachment
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            OwnerType = "operation",
            OwnerId = _ownerId,
            FileName = "photo.png",
            ContentType = "image/png",
            SizeBytes = 12,
            StorageKey = "ab/cd/key.png",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        _repository.Setup(r => r.ListForOwnerAsync("operation", _ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entity });

        // Act
        var result = await CreateSut().Handle(new ListAttachmentsRequest("operation", _ownerId), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.Single().FileName.Should().Be("photo.png");
        result.Single().ContentType.Should().Be("image/png");
    }
}
