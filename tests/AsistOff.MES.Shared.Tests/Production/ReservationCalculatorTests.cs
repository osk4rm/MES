using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Production;

/// <summary>
/// Unit tests for the soft reservation math (issue #291): requirement
/// aggregation from BOM items, the re-release idempotency guard and FIFO
/// relief from RW confirmation lines.
/// </summary>
public sealed class ReservationCalculatorTests
{
    private static ProductionOrder Order(decimal plannedQuantity = 100m) => new()
    {
        Id = Guid.NewGuid(),
        Code = "PO-001",
        ProductId = Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        RecipeVersionId = Guid.NewGuid(),
        PlannedQuantity = plannedQuantity,
        Status = ProductionOrderStatus.Planned
    };

    private static BomItem Item(
        Guid productId, decimal quantity,
        BomQuantityType type = BomQuantityType.PerUnit,
        decimal? scrap = null, Guid? warehouseId = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        OperationNodeId = Guid.NewGuid(),
        ProductId = productId,
        Quantity = quantity,
        QuantityType = type,
        ScrapPercentage = scrap,
        PreferredWarehouseId = warehouseId
    };

    private static MaterialReservation Reservation(
        Guid productId, Guid? warehouseId, decimal reserved, decimal relieved = 0m,
        ReservationStatus status = ReservationStatus.Active) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        ProductionOrderId = Guid.NewGuid(),
        ProductId = productId,
        WarehouseId = warehouseId,
        QuantityReserved = reserved,
        QuantityRelieved = relieved,
        Status = status,
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public void BuildRequirements_PerUnitWithScrap_ScalesByPlannedQuantity()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var order = Order(plannedQuantity: 100m);
        var items = new List<BomItem> { Item(productId, 2m, scrap: 10m) };

        // Act
        var result = ReservationCalculator.BuildRequirements(order, items);

        // Assert — 2 per unit over 100 pcs plus 10% scrap = 220.
        result.Should().ContainSingle()
            .Which.Should().Be(new ReservationRequirement(productId, null, 220m));
    }

    [Fact]
    public void BuildRequirements_PerBatch_IgnoresPlannedQuantity()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var order = Order(plannedQuantity: 100m);
        var items = new List<BomItem> { Item(productId, 5m, BomQuantityType.PerBatch, scrap: 10m) };

        // Act
        var result = ReservationCalculator.BuildRequirements(order, items);

        // Assert — one batch per order: 5 plus 10% scrap = 5.5.
        result.Should().ContainSingle()
            .Which.Quantity.Should().Be(5.5m);
    }

    [Fact]
    public void BuildRequirements_GroupsByProductAndWarehouse()
    {
        // Arrange
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        var warehouse = Guid.NewGuid();
        var order = Order(plannedQuantity: 10m);
        var items = new List<BomItem>
        {
            Item(productA, 2m, warehouseId: warehouse),
            Item(productA, 3m, warehouseId: warehouse),
            Item(productA, 1m, warehouseId: null),
            Item(productB, 4m, warehouseId: warehouse)
        };

        // Act
        var result = ReservationCalculator.BuildRequirements(order, items);

        // Assert
        result.Should().HaveCount(3);
        result.Should().ContainSingle(x =>
            x.ProductId == productA && x.WarehouseId == warehouse && x.Quantity == 50m);
        result.Should().ContainSingle(x =>
            x.ProductId == productA && x.WarehouseId == null && x.Quantity == 10m);
        result.Should().ContainSingle(x =>
            x.ProductId == productB && x.WarehouseId == warehouse && x.Quantity == 40m);
    }

    [Fact]
    public void BuildRequirements_NoBomItems_ReturnsEmpty()
    {
        // Arrange
        var order = Order();

        // Act
        var result = ReservationCalculator.BuildRequirements(order, new List<BomItem>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildRequirements_NonPositiveQuantity_ThrowsValidationException()
    {
        // Arrange
        var order = Order();
        var items = new List<BomItem> { Item(Guid.NewGuid(), 0m) };

        // Act
        var act = () => ReservationCalculator.BuildRequirements(order, items);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void ExcludeAlreadyReserved_SkipsExistingPairsIncludingNullWarehouse()
    {
        // Arrange
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        var warehouse = Guid.NewGuid();
        var requirements = new List<ReservationRequirement>
        {
            new(productA, null, 10m),
            new(productA, warehouse, 20m),
            new(productB, warehouse, 30m)
        };
        var existing = new List<MaterialReservation>
        {
            Reservation(productA, null, 10m),
            Reservation(productB, warehouse, 30m, status: ReservationStatus.PartiallyRelieved)
        };

        // Act
        var result = ReservationCalculator.ExcludeAlreadyReserved(requirements, existing);

        // Assert
        result.Should().ContainSingle()
            .Which.Should().Be(new ReservationRequirement(productA, warehouse, 20m));
    }

    [Fact]
    public void ExcludeAlreadyReserved_NoExisting_ReturnsAll()
    {
        // Arrange
        var requirements = new List<ReservationRequirement>
        {
            new(Guid.NewGuid(), null, 10m)
        };

        // Act
        var result = ReservationCalculator.ExcludeAlreadyReserved(requirements, new List<MaterialReservation>());

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public void ApplyRelief_PartialRelief_FlipsToPartiallyRelieved()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var reservation = Reservation(productId, warehouseId, reserved: 200m);
        var lines = new List<MovementPreviewLine>
        {
            new(MovementCalculator.IssueType, productId, 20m, null, warehouseId)
        };

        // Act
        ReservationCalculator.ApplyRelief(new[] { reservation }, lines);

        // Assert
        reservation.QuantityRelieved.Should().Be(20m);
        reservation.Status.Should().Be(ReservationStatus.PartiallyRelieved);
    }

    [Fact]
    public void ApplyRelief_FullRelief_ClosesReservation()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var reservation = Reservation(productId, null, reserved: 20m, relieved: 5m,
            status: ReservationStatus.PartiallyRelieved);
        var lines = new List<MovementPreviewLine>
        {
            new(MovementCalculator.IssueType, productId, 15m, null, null)
        };

        // Act
        ReservationCalculator.ApplyRelief(new[] { reservation }, lines);

        // Assert
        reservation.QuantityRelieved.Should().Be(20m);
        reservation.Status.Should().Be(ReservationStatus.Closed);
    }

    [Fact]
    public void ApplyRelief_OverConfirmation_CapsAtReserved()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var reservation = Reservation(productId, null, reserved: 20m);
        var lines = new List<MovementPreviewLine>
        {
            new(MovementCalculator.IssueType, productId, 300m, null, null)
        };

        // Act
        ReservationCalculator.ApplyRelief(new[] { reservation }, lines);

        // Assert — over-confirmation closes the reservation without going negative.
        reservation.QuantityRelieved.Should().Be(20m);
        reservation.RemainingQuantity.Should().Be(0m);
        reservation.Status.Should().Be(ReservationStatus.Closed);
    }

    [Fact]
    public void ApplyRelief_IgnoresPwLinesAndForeignPairs()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var reservation = Reservation(productId, null, reserved: 20m);
        var lines = new List<MovementPreviewLine>
        {
            new(MovementCalculator.ReceiptType, productId, 10m, null, null),
            new(MovementCalculator.IssueType, Guid.NewGuid(), 10m, null, null),
            new(MovementCalculator.IssueType, productId, 10m, null, Guid.NewGuid())
        };

        // Act
        ReservationCalculator.ApplyRelief(new[] { reservation }, lines);

        // Assert
        reservation.QuantityRelieved.Should().Be(0m);
        reservation.Status.Should().Be(ReservationStatus.Active);
    }

    [Fact]
    public void ApplyRelief_RelievesOldestFirst()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var older = Reservation(productId, null, reserved: 10m);
        older.CreatedAt = new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc);
        var newer = Reservation(productId, null, reserved: 100m);
        newer.CreatedAt = new DateTime(2026, 9, 21, 8, 0, 0, DateTimeKind.Utc);
        var lines = new List<MovementPreviewLine>
        {
            new(MovementCalculator.IssueType, productId, 30m, null, null)
        };

        // Act — passed newest-first; FIFO still relieves the oldest first.
        ReservationCalculator.ApplyRelief(new[] { newer, older }, lines);

        // Assert
        older.Status.Should().Be(ReservationStatus.Closed);
        older.QuantityRelieved.Should().Be(10m);
        newer.Status.Should().Be(ReservationStatus.PartiallyRelieved);
        newer.QuantityRelieved.Should().Be(20m);
    }
}
