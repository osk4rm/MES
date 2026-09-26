using AsistOff.MES.Configuration.Application.Features.StockMovements.Responses;
using AsistOff.MES.Configuration.Application.Features.StockMovements.StockOnHand;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class GetStockOnHandRequestHandlerTests
{
    private readonly Mock<IStockMovementsRepository> _movements = new();
    private readonly Mock<IMaterialReservationsRepository> _reservations = new();

    public GetStockOnHandRequestHandlerTests()
    {
        _reservations.Setup(r => r.ListOpenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MaterialReservation>());
    }

    private GetStockOnHandRequestHandler CreateSut() => new(_movements.Object, _reservations.Object);

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
            .Which.Should().Be(new StockOnHandResponse(productId, warehouseId, 6m, 0m, 6m));
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
            .Which.Should().Be(new StockOnHandResponse(wanted, null, 5m, 0m, 5m));
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
            .Which.Should().Be(new StockOnHandResponse(productId, wanted, 5m, 0m, 5m));
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
    public async Task Handle_OpenReservations_ReduceAvailableQuantity()
    {
        // Arrange — on hand 10, open reservation remainder 4 => available 6.
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        _movements.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Line(StockMovement.ReceiptType, productId, warehouseId, 10m)
            ]);
        _reservations.Setup(r => r.ListOpenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                OpenReservation(productId, warehouseId, reserved: 10m, relieved: 6m),
                OpenReservation(productId, null, reserved: 5m, relieved: 0m)
            ]);

        // Act
        var result = await CreateSut().Handle(new GetStockOnHandRequest(), CancellationToken.None);

        // Assert — only the same-pair reservation counts; the null-warehouse
        // row has no stock row and stays invisible.
        result.Should().ContainSingle()
            .Which.Should().Be(new StockOnHandResponse(productId, warehouseId, 10m, 4m, 6m));
    }

    [Fact]
    public async Task Handle_ReservationExceedingOnHand_ReportsNegativeAvailable()
    {
        // Arrange — soft allocation only: shortages surface as negative
        // availability instead of blocking.
        var productId = Guid.NewGuid();
        _movements.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Line(StockMovement.ReceiptType, productId, null, 10m),
                Line(StockMovement.IssueType, productId, null, 4m)
            ]);
        _reservations.Setup(r => r.ListOpenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                OpenReservation(productId, null, reserved: 20m, relieved: 0m)
            ]);

        // Act
        var result = await CreateSut().Handle(new GetStockOnHandRequest(), CancellationToken.None);

        // Assert — on hand 6, reserved 20 => available -14.
        result.Should().ContainSingle()
            .Which.Should().Be(new StockOnHandResponse(productId, null, 6m, 20m, -14m));
    }

    private static MaterialReservation OpenReservation(
        Guid productId, Guid? warehouseId, decimal reserved, decimal relieved) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        ProductionOrderId = Guid.NewGuid(),
        ProductId = productId,
        WarehouseId = warehouseId,
        QuantityReserved = reserved,
        QuantityRelieved = relieved,
        Status = ReservationStatus.Active,
        CreatedAt = DateTime.UtcNow
    };

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
