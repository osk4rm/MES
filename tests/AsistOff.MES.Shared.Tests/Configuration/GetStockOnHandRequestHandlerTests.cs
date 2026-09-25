using AsistOff.MES.Configuration.Application.Features.StockMovements.Responses;
using AsistOff.MES.Configuration.Application.Features.StockMovements.StockOnHand;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class GetStockOnHandRequestHandlerTests
{
    private readonly Mock<IStockMovementsRepository> _movements = new();

    private GetStockOnHandRequestHandler CreateSut() => new(_movements.Object);

    private static StockMovement Line(string type, Guid productId, Guid? warehouseId, decimal quantity) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        MovementType = type,
        ProductId = productId,
        Quantity = quantity,
        WarehouseId = warehouseId,
        ProductionConfirmationId = Guid.NewGuid(),
        ProductionOrderId = Guid.NewGuid(),
        ReportedAt = DateTime.UtcNow
    };

    [Fact]
    public void Request_ImplementsTenantRequest()
    {
        typeof(GetStockOnHandRequest).Should().Implement<ITenantRequest<IReadOnlyList<StockOnHandResponse>>>();
    }

    [Fact]
    public async Task Handle_PwReceiptAndRwIssueForSamePair_ReportsNetBalance()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        _movements.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Line(StockMovement.ReceiptType, productId, warehouseId, 10m),
                Line(StockMovement.IssueType, productId, warehouseId, 4m)
            ]);

        // Act
        var result = await CreateSut().Handle(new GetStockOnHandRequest(), CancellationToken.None);

        // Assert
        result.Should().ContainSingle()
            .Which.Should().Be(new StockOnHandResponse(productId, warehouseId, 6m));
    }

    [Fact]
    public async Task Handle_NullWarehouseLines_AggregatedUnderUnassignedBucket()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        _movements.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Line(StockMovement.ReceiptType, productId, null, 10m),
                Line(StockMovement.IssueType, productId, null, 3m),
                Line(StockMovement.IssueType, productId, warehouseId, 4m)
            ]);

        // Act
        var result = await CreateSut().Handle(new GetStockOnHandRequest(), CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainSingle(x => x.ProductId == productId && x.WarehouseId == null && x.QuantityOnHand == 7m);
        result.Should().ContainSingle(x => x.ProductId == productId && x.WarehouseId == warehouseId && x.QuantityOnHand == -4m);
    }

    [Fact]
    public async Task Handle_ProductIdFilter_NarrowsToMatchingProduct()
    {
        // Arrange
        var wanted = Guid.NewGuid();
        var other = Guid.NewGuid();
        _movements.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Line(StockMovement.ReceiptType, wanted, null, 5m),
                Line(StockMovement.ReceiptType, other, null, 9m)
            ]);

        // Act
        var result = await CreateSut().Handle(
            new GetStockOnHandRequest { ProductId = wanted }, CancellationToken.None);

        // Assert
        result.Should().ContainSingle()
            .Which.Should().Be(new StockOnHandResponse(wanted, null, 5m));
    }

    [Fact]
    public async Task Handle_WarehouseIdFilter_NarrowsToMatchingWarehouse()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var wanted = Guid.NewGuid();
        _movements.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Line(StockMovement.ReceiptType, productId, wanted, 5m),
                Line(StockMovement.ReceiptType, productId, null, 9m)
            ]);

        // Act
        var result = await CreateSut().Handle(
            new GetStockOnHandRequest { WarehouseId = wanted }, CancellationToken.None);

        // Assert
        result.Should().ContainSingle()
            .Which.Should().Be(new StockOnHandResponse(productId, wanted, 5m));
    }

    [Fact]
    public async Task Handle_NoFilters_ReturnsAllBalances()
    {
        // Arrange
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        _movements.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Line(StockMovement.ReceiptType, first, null, 5m),
                Line(StockMovement.ReceiptType, second, warehouseId, 9m)
            ]);

        // Act
        var result = await CreateSut().Handle(new GetStockOnHandRequest(), CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Validate_EmptyGuidFilters_AreInvalid()
    {
        // Arrange
        var validator = new GetStockOnHandRequestValidator();

        // Act
        var result = await validator.ValidateRequestAsync(
            new GetStockOnHandRequest { ProductId = Guid.Empty, WarehouseId = Guid.Empty });

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_NoFilters_IsValid()
    {
        // Arrange
        var validator = new GetStockOnHandRequestValidator();

        // Act
        var result = await validator.ValidateRequestAsync(new GetStockOnHandRequest());

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_NonEmptyGuidFilters_AreValid()
    {
        // Arrange
        var validator = new GetStockOnHandRequestValidator();

        // Act
        var result = await validator.ValidateRequestAsync(
            new GetStockOnHandRequest { ProductId = Guid.NewGuid(), WarehouseId = Guid.NewGuid() });

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
