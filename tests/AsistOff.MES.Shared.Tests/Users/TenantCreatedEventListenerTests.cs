using AsistOff.MES.Multitenancy.Contracts.Events;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Users.Application.EventListeners;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Rbac;
using AsistOff.MES.Users.Core.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Users;

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
}
