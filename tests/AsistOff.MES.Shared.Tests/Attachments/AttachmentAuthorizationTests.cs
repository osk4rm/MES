using AsistOff.MES.Attachments.Application.Features.Common;
using AsistOff.MES.Attachments.Application.Features.Delete;
using AsistOff.MES.Attachments.Application.Features.Download;
using AsistOff.MES.Attachments.Application.Features.List;
using AsistOff.MES.Attachments.Domain.Entities;
using AsistOff.MES.Attachments.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Storage;
using AsistOff.MES.Users.Core.Rbac;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Attachments;

/// <summary>
/// Unit tests for issue #315 (attachment object-level authorization):
/// permission declarations plus the owner-scope rule enforced in the
/// list/download/delete handlers before any bytes stream or any row is
/// removed.
/// </summary>
public class AttachmentAuthorizationTests
{
    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03];

    [Fact]
    public void ListAttachmentsRequest_RequiresAttachmentsRead()
    {
        // Arrange & Act
        var permissions = typeof(ListAttachmentsRequest)
            .GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true)
            .Cast<RequirePermissionAttribute>()
            .Select(a => a.Permission)
            .ToList();

        // Assert — derived from RbacDefaults, no role-string literals.
        permissions.Should().Contain(RbacDefaults.AttachmentsRead);
    }

    [Fact]
    public void DownloadAttachmentRequest_RequiresAttachmentsRead()
    {
        // Arrange & Act
        var permissions = typeof(DownloadAttachmentRequest)
            .GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true)
            .Cast<RequirePermissionAttribute>()
            .Select(a => a.Permission)
            .ToList();

        // Assert
        permissions.Should().Contain(RbacDefaults.AttachmentsRead);
    }

    [Fact]
    public void DeleteAttachmentRequest_RequiresAttachmentsWrite()
    {
        // Arrange & Act
        var permissions = typeof(DeleteAttachmentRequest)
            .GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true)
            .Cast<RequirePermissionAttribute>()
            .Select(a => a.Permission)
            .ToList();

        // Assert
        permissions.Should().Contain(RbacDefaults.AttachmentsWrite);
    }

    [Theory]
    [InlineData("operation", "production.read")]
    [InlineData("operationnode", "production.read")]
    [InlineData("recipeversion", "production.read")]
    [InlineData("productionorder", "production.read")]
    [InlineData("confirmation", "production.read")]
    [InlineData("productionconfirmation", "production.read")]
    [InlineData("lot", "production.read")]
    [InlineData("machine", "configuration.read")]
    [InlineData("workcenter", "configuration.read")]
    [InlineData("product", "configuration.read")]
    [InlineData("warehouse", "configuration.read")]
    public void GetRequiredPermission_OwnerType_MapsToModuleRead(string ownerType, string expected)
    {
        // Arrange & Act
        var actual = AttachmentScopePolicy.GetRequiredPermission(ownerType);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public void GetRequiredPermission_UnknownOwnerType_ReturnsNull()
    {
        // Arrange & Act
        var actual = AttachmentScopePolicy.GetRequiredPermission("starship");

        // Assert — no extra scope; the pipeline-level attachments permission applies.
        actual.Should().BeNull();
    }

    [Fact]
    public async Task List_OutOfScopeOwner_ThrowsForbiddenWithoutQuerying()
    {
        // Arrange — caller holds attachments.read but not the production scope.
        var ownerId = Guid.NewGuid();
        var repository = new Mock<IAttachmentsRepository>();
        var owners = new Mock<IAttachmentOwnerVerifier>();
        owners.Setup(o => o.ExistsAsync("operation", ownerId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var permissions = PermissionsWith(RbacDefaults.AttachmentsRead, RbacDefaults.ConfigurationRead);
        var sut = new ListAttachmentsRequestHandler(repository.Object, owners.Object, permissions.Object);

        // Act
        var act = () => sut.Handle(new ListAttachmentsRequest("operation", ownerId), CancellationToken.None);

        // Assert — denied before any attachment rows are read.
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*production.read*");
        repository.Verify(r => r.ListForOwnerAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task List_InScopeOwner_ReturnsAttachments()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var repository = new Mock<IAttachmentsRepository>();
        repository.Setup(r => r.ListForOwnerAsync("operation", ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Attachment("operation", ownerId) });
        var owners = new Mock<IAttachmentOwnerVerifier>();
        owners.Setup(o => o.ExistsAsync("operation", ownerId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var permissions = PermissionsWith(RbacDefaults.AttachmentsRead, RbacDefaults.ProductionRead);
        var sut = new ListAttachmentsRequestHandler(repository.Object, owners.Object, permissions.Object);

        // Act
        var result = await sut.Handle(new ListAttachmentsRequest("operation", ownerId), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task Download_OutOfScopeAttachment_ThrowsForbiddenWithoutOpeningStorage()
    {
        // Arrange — attachment linked to a production owner; caller lacks production.read.
        var id = Guid.NewGuid();
        var repository = new Mock<IAttachmentsRepository>();
        repository.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Attachment("operation", Guid.NewGuid(), id));
        var storage = new Mock<IFileStorage>();
        var permissions = PermissionsWith(RbacDefaults.AttachmentsRead, RbacDefaults.ConfigurationRead);
        var sut = new DownloadAttachmentRequestHandler(repository.Object, storage.Object, permissions.Object);

        // Act
        var act = () => sut.Handle(new DownloadAttachmentRequest(id), CancellationToken.None);

        // Assert — denied with no file bytes streamed.
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*production.read*");
        storage.Verify(s => s.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Download_InScopeAttachment_ReturnsContent()
    {
        // Arrange
        var id = Guid.NewGuid();
        var repository = new Mock<IAttachmentsRepository>();
        repository.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Attachment("operation", Guid.NewGuid(), id));
        var storage = new Mock<IFileStorage>();
        storage.Setup(s => s.OpenReadAsync("ab/cd/key.png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(PngBytes));
        var permissions = PermissionsWith(RbacDefaults.AttachmentsRead, RbacDefaults.ProductionRead);
        var sut = new DownloadAttachmentRequestHandler(repository.Object, storage.Object, permissions.Object);

        // Act
        var result = await sut.Handle(new DownloadAttachmentRequest(id), CancellationToken.None);

        // Assert
        result.FileName.Should().Be("photo.png");
        storage.Verify(s => s.OpenReadAsync("ab/cd/key.png", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_OutOfScopeAttachment_ThrowsForbiddenWithoutDeleting()
    {
        // Arrange — caller holds attachments.write but not the production scope.
        var id = Guid.NewGuid();
        var repository = new Mock<IAttachmentsRepository>();
        repository.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Attachment("operation", Guid.NewGuid(), id));
        var storage = new Mock<IFileStorage>();
        var permissions = PermissionsWith(RbacDefaults.AttachmentsWrite, RbacDefaults.ConfigurationRead);
        var sut = new DeleteAttachmentRequestHandler(repository.Object, storage.Object, permissions.Object);

        // Act
        var act = () => sut.Handle(new DeleteAttachmentRequest(id), CancellationToken.None);

        // Assert — the record is left untouched.
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*production.read*");
        repository.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        storage.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_InScopeAttachment_RemovesRecord()
    {
        // Arrange
        var id = Guid.NewGuid();
        var repository = new Mock<IAttachmentsRepository>();
        repository.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Attachment("operation", Guid.NewGuid(), id));
        var storage = new Mock<IFileStorage>();
        var permissions = PermissionsWith(RbacDefaults.AttachmentsWrite, RbacDefaults.ProductionRead);
        var sut = new DeleteAttachmentRequestHandler(repository.Object, storage.Object, permissions.Object);

        // Act
        await sut.Handle(new DeleteAttachmentRequest(id), CancellationToken.None);

        // Assert
        repository.Verify(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<ICurrentPermissionsAccessor> PermissionsWith(params string[] permissions)
    {
        var mock = new Mock<ICurrentPermissionsAccessor>();
        mock.SetupGet(p => p.Permissions).Returns(permissions);
        return mock;
    }

    private static Attachment Attachment(string ownerType, Guid ownerId, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        OwnerType = ownerType,
        OwnerId = ownerId,
        FileName = "photo.png",
        ContentType = "image/png",
        SizeBytes = PngBytes.Length,
        StorageKey = "ab/cd/key.png",
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };
}
