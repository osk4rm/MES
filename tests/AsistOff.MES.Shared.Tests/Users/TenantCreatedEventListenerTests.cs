using AsistOff.MES.Multitenancy.Contracts.Events;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Users.Application.EventListeners;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Rbac;
using AsistOff.MES.Users.Core.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Users;

/// <summary>
/// Slice 3 (#260) idempotency coverage: redelivering the same tenant-created
/// event provisions exactly once (no duplicate admin users; the provisioner
/// reconciles by explicit tenant id), malformed events are rejected before
/// any provisioning so the relay parks them as poison without partial RBAC
/// writes, and the MediatR relay entry converges on the same handling.
/// </summary>
public class TenantCreatedEventListenerTests
{
    private readonly Mock<IUsersRepository> _users = new();
    private readonly Mock<IRbacProvisioner> _provisioner = new();
    private readonly Mock<IGuidProvider> _guids = new();

    private TenantCreatedEventListener CreateSut() =>
        new(_users.Object, _provisioner.Object, _guids.Object, NullLogger<TenantCreatedEventListener>.Instance);

    [Fact]
    public async Task Handle_ProvisionsRbac_BeforeCreatingUser()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var order = new List<string>();
        _provisioner.Setup(p => p.ProvisionAsync(tenantId, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("provision"))
            .Returns(Task.CompletedTask);
        _users.Setup(r => r.GetByEmailAndTenantIgnoringQueryFiltersAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("lookup"))
            .ReturnsAsync((User?)null);
        _users.Setup(r => r.AddAsync(It.IsAny<User>()))
            .Callback(() => order.Add("add"))
            .Returns(Task.CompletedTask);
        _guids.Setup(g => g.NewGuid()).Returns(Guid.NewGuid());

        // Act
        await CreateSut().HandleAsync(new TenantCreatedEvent(tenantId, "admin@x.local", "hashed"));

        // Assert
        _provisioner.Verify(p => p.ProvisionAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(r => r.AddAsync(It.Is<User>(u => u.TenantId == tenantId && u.IsTenantAdmin)), Times.Once);
        order.Should().Equal("provision", "lookup", "add");
    }

    [Fact]
    public async Task Handle_WhenUserExists_SkipsCreation_ButStillProvisions()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _users.Setup(r => r.GetByEmailAndTenantIgnoringQueryFiltersAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = Guid.NewGuid(), TenantId = tenantId, Email = "a@x", Password = "h" });

        // Act
        await CreateSut().HandleAsync(new TenantCreatedEvent(tenantId, "a@x", "hashed"));

        // Assert
        _provisioner.Verify(p => p.ProvisionAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DuplicateDelivery_ProvisionsOnce_NoDuplicateUser()
    {
        // Arrange — the relay redelivers the same event (at-least-once): the
        // first delivery creates the admin, the second finds it.
        var tenantId = Guid.NewGuid();
        var created = new User { Id = Guid.NewGuid(), TenantId = tenantId, Email = "admin@x.local", Password = "hashed" };
        _users.SetupSequence(r => r.GetByEmailAndTenantIgnoringQueryFiltersAsync(
                "admin@x.local", tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null)
            .ReturnsAsync(created);
        _users.Setup(r => r.AddAsync(It.IsAny<User>()))
            .Returns(Task.CompletedTask);
        _guids.Setup(g => g.NewGuid()).Returns(created.Id);
        var sut = CreateSut();
        var @event = new TenantCreatedEvent(tenantId, "admin@x.local", "hashed");

        // Act — redeliver the same event twice.
        await sut.HandleAsync(@event);
        await sut.HandleAsync(@event);

        // Assert — exactly one admin user; the provisioner reconciles by
        // explicit tenant id (idempotent), so roles are provisioned once.
        _users.Verify(r => r.AddAsync(It.Is<User>(u => u.TenantId == tenantId && u.IsTenantAdmin)), Times.Once);
        _users.Verify(r => r.GetByEmailAndTenantIgnoringQueryFiltersAsync(
            "admin@x.local", tenantId, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Theory]
    [InlineData("")]           // empty email
    [InlineData("   ")]        // whitespace email
    public async Task Handle_BlankEmail_ThrowsValidation_BeforeProvisioning(string email)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => sut.HandleAsync(new TenantCreatedEvent(Guid.NewGuid(), email, "hashed"));

        // Assert — rejected before any RBAC write, so the relay parks the row
        // as poison without partial provisioning.
        await act.Should().ThrowAsync<ValidationException>();
        _provisioner.Verify(p => p.ProvisionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _users.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EmptyTenantId_ThrowsValidation_BeforeProvisioning()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => sut.HandleAsync(new TenantCreatedEvent(Guid.Empty, "admin@x.local", "hashed"));

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _provisioner.Verify(p => p.ProvisionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _users.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task HandleViaRelay_MediatRHandler_ProvisionsSameAsDirectPath()
    {
        // Arrange — the outbox relay delivers through MediatR, not the direct
        // event dispatcher.
        var tenantId = Guid.NewGuid();
        _users.Setup(r => r.GetByEmailAndTenantIgnoringQueryFiltersAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _users.Setup(r => r.AddAsync(It.IsAny<User>()))
            .Returns(Task.CompletedTask);
        _guids.Setup(g => g.NewGuid()).Returns(Guid.NewGuid());

        // Act
        await CreateSut().Handle(new TenantCreatedEvent(tenantId, "admin@x.local", "hashed"), CancellationToken.None);

        // Assert
        _provisioner.Verify(p => p.ProvisionAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(r => r.AddAsync(It.Is<User>(u => u.TenantId == tenantId && u.IsTenantAdmin)), Times.Once);
    }

    [Fact]
    public async Task HandleViaRelay_MalformedEvent_ThrowsBeforeProvisioning()
    {
        // Arrange
        var sut = CreateSut();

        // Act — corrupt relay payload surfaces as a malformed event.
        var act = () => sut.Handle(new TenantCreatedEvent(Guid.Empty, string.Empty, string.Empty), CancellationToken.None);

        // Assert — the relay retries then parks poison; nothing is
        // half-provisioned.
        await act.Should().ThrowAsync<ValidationException>();
        _provisioner.Verify(p => p.ProvisionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _users.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
    }
}
