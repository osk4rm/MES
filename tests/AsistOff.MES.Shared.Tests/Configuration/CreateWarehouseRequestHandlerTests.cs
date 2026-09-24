using AsistOff.MES.Configuration.Application.Features.Warehouses.Create;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class CreateWarehouseRequestHandlerTests
{
    private readonly Mock<IWarehousesRepository> _repository = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<ITenantContext> _tenant = new();

    public CreateWarehouseRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _tenant.SetupGet(t => t.TenantId).Returns(Guid.NewGuid());
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Warehouse>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Warehouse w, CancellationToken _) => w);
    }

    private CreateWarehouseRequestHandler CreateSut() =>
        new(_repository.Object, _guids.Object, _tenant.Object, NullLogger<CreateWarehouseRequestHandler>.Instance);

    [Fact]
    public async Task Handle_NullSyncId_PersistsNullSyncId()
    {
        // Arrange
        Warehouse? persisted = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Warehouse>(), It.IsAny<CancellationToken>()))
            .Callback<Warehouse, CancellationToken>((w, _) => persisted = w)
            .ReturnsAsync((Warehouse w, CancellationToken _) => w);

        var request = new CreateWarehouseRequest("Main", null);

        // Act
        var result = await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.SyncId.Should().BeNull();
        persisted.Name.Should().Be("Main");
        result.SyncId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_EmptySyncId_NormalizesToNull()
    {
        // Arrange
        Warehouse? persisted = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Warehouse>(), It.IsAny<CancellationToken>()))
            .Callback<Warehouse, CancellationToken>((w, _) => persisted = w)
            .ReturnsAsync((Warehouse w, CancellationToken _) => w);

        var request = new CreateWarehouseRequest("Main", "  ");

        // Act
        await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.SyncId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_EmptyName_ThrowsValidationException()
    {
        // Arrange
        var request = new CreateWarehouseRequest("  ", null);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_WithSyncId_PersistsSyncId()
    {
        // Arrange
        Warehouse? persisted = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Warehouse>(), It.IsAny<CancellationToken>()))
            .Callback<Warehouse, CancellationToken>((w, _) => persisted = w)
            .ReturnsAsync((Warehouse w, CancellationToken _) => w);

        var request = new CreateWarehouseRequest("Main", "ERP-001");

        // Act
        var result = await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        persisted!.SyncId.Should().Be("ERP-001");
        result.SyncId.Should().Be("ERP-001");
    }
}
