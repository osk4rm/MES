using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Application.Features.ProductionConfirmations;
using AsistOff.MES.Production.Application.Features.ProductionConfirmations.Create;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class CreateProductionConfirmationRequestHandlerTests
{
    private readonly Mock<IProductionConfirmationsRepository> _confirmations = new();
    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private readonly Mock<IChildEntitiesRepository> _children = new();
    private readonly Mock<IStockMovementsRepository> _movements = new();
    private readonly Mock<ILotsRepository> _lots = new();
    private readonly Mock<ILotGenealogyEdgesRepository> _edges = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    public CreateProductionConfirmationRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
        _children.Setup(r => r.ListBomItemsForVersionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BomItem>());
        _unitOfWork.Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<Task<ProductionConfirmationResponse>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task<ProductionConfirmationResponse>> action, CancellationToken _) => action());
    }

    private CreateProductionConfirmationRequestHandler CreateSut() =>
        new(_confirmations.Object, _orders.Object, _children.Object, _movements.Object, _lots.Object, _edges.Object, _guids.Object, _clock.Object, _tenant.Object, _unitOfWork.Object);

    private static ProductionOrder ReleasedOrder() => new()
    {
        Id = Guid.NewGuid(),
        Code = "PO-001",
        ProductId = Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        RecipeVersionId = Guid.NewGuid(),
        PlannedQuantity = 100m,
        Status = ProductionOrderStatus.Released,
        ReleasedAt = new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc)
    };

    private CreateProductionConfirmationRequest ValidRequest(Guid orderId) => new(
        orderId,
        Guid.NewGuid(),
        null,
        new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc),
        10m,
        2m,
        null);

    private void SetupOrder(ProductionOrder order) =>
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

    [Fact]
    public async Task Handle_ReleasedOrder_CreatesConfirmationAndTransitionsToInProgress()
    {
        var order = ReleasedOrder();
        SetupOrder(order);
        ProductionConfirmation? saved = null;
        _confirmations.Setup(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()))
            .Callback<ProductionConfirmation, CancellationToken>((e, _) => saved = e)
            .ReturnsAsync((ProductionConfirmation e, CancellationToken _) => e);

        var result = await CreateSut().Handle(ValidRequest(order.Id), CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.ProductionOrderId.Should().Be(order.Id);
        saved.TenantId.Should().Be(_tenantId);
        saved.GoodQuantity.Should().Be(10m);
        saved.ScrapQuantity.Should().Be(2m);
        order.Status.Should().Be(ProductionOrderStatus.InProgress);
        _orders.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        result.ProductionOrderId.Should().Be(order.Id);
    }

    [Fact]
    public async Task Handle_InProgressOrder_KeepsInProgress()
    {
        var order = ReleasedOrder();
        order.Status = ProductionOrderStatus.InProgress;
        SetupOrder(order);

        await CreateSut().Handle(ValidRequest(order.Id), CancellationToken.None);

        order.Status.Should().Be(ProductionOrderStatus.InProgress);
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PlannedOrder_ThrowsValidationException()
    {
        var order = ReleasedOrder();
        order.Status = ProductionOrderStatus.Planned;
        SetupOrder(order);

        var act = () => CreateSut().Handle(ValidRequest(order.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_CompletedOrder_ThrowsConflictException()
    {
        var order = ReleasedOrder();
        order.Status = ProductionOrderStatus.Completed;
        SetupOrder(order);

        var act = () => CreateSut().Handle(ValidRequest(order.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_ClosedOrder_ThrowsConflictException()
    {
        var order = ReleasedOrder();
        order.Status = ProductionOrderStatus.Closed;
        SetupOrder(order);

        var act = () => CreateSut().Handle(ValidRequest(order.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_UnknownOrder_ThrowsNotFoundException()
    {
        var orderId = Guid.NewGuid();
        _orders.Setup(r => r.GetAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductionOrder?)null);

        var act = () => CreateSut().Handle(ValidRequest(orderId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ZeroTotalQuantity_ThrowsValidationException()
    {
        var order = ReleasedOrder();
        SetupOrder(order);
        var request = ValidRequest(order.Id) with { GoodQuantity = 0m, ScrapQuantity = 0m };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_NegativeQuantity_ThrowsValidationException()
    {
        var order = ReleasedOrder();
        SetupOrder(order);
        var request = ValidRequest(order.Id) with { GoodQuantity = -1m };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_ScrapOnlyConfirmation_IsAccepted()
    {
        var order = ReleasedOrder();
        SetupOrder(order);
        var request = ValidRequest(order.Id) with { GoodQuantity = 0m, ScrapQuantity = 3m };

        var result = await CreateSut().Handle(request, CancellationToken.None);

        result.ScrapQuantity.Should().Be(3m);
        result.GoodQuantity.Should().Be(0m);
    }

    [Fact]
    public async Task Handle_FutureReportedAt_ThrowsValidationException()
    {
        var order = ReleasedOrder();
        SetupOrder(order);
        var request = ValidRequest(order.Id) with { ReportedAt = _now.AddHours(1) };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_ReportedAtBeforeReleasedAt_ThrowsValidationException()
    {
        var order = ReleasedOrder();
        SetupOrder(order);
        var request = ValidRequest(order.Id) with { ReportedAt = order.ReleasedAt!.Value.AddMinutes(-1) };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_EmptyMachineId_ThrowsValidationException()
    {
        var order = ReleasedOrder();
        SetupOrder(order);
        var request = ValidRequest(order.Id) with { MachineId = Guid.Empty };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_HappyPath_PostsPreviewEquivalentLedgerLines()
    {
        // Arrange
        var order = ReleasedOrder();
        order.MeasureUnitId = Guid.NewGuid();
        SetupOrder(order);
        var bomProductId = Guid.NewGuid();
        var bomMeasureUnitId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var bomItems = new List<BomItem>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                OperationNodeId = Guid.NewGuid(),
                ProductId = bomProductId,
                MeasureUnitId = bomMeasureUnitId,
                Quantity = 2m,
                QuantityType = BomQuantityType.PerUnit,
                PreferredWarehouseId = warehouseId,
                SortIndex = 0
            }
        };
        _children.Setup(r => r.ListBomItemsForVersionAsync(order.RecipeVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bomItems);
        IReadOnlyCollection<StockMovement>? posted = null;
        _movements.Setup(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<StockMovement>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<StockMovement>, CancellationToken>((e, _) => posted = e)
            .Returns(Task.CompletedTask);
        ProductionConfirmation? saved = null;
        _confirmations.Setup(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()))
            .Callback<ProductionConfirmation, CancellationToken>((e, _) => saved = e)
            .ReturnsAsync((ProductionConfirmation e, CancellationToken _) => e);

        // Act
        await CreateSut().Handle(ValidRequest(order.Id), CancellationToken.None);

        // Assert
        var expected = MovementCalculator.BuildPreview(order, 10m, 1, bomItems);
        posted.Should().NotBeNull();
        posted.Should().HaveCount(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            posted!.ElementAt(i).MovementType.Should().Be(expected[i].MovementType);
            posted!.ElementAt(i).ProductId.Should().Be(expected[i].ProductId);
            posted!.ElementAt(i).Quantity.Should().Be(expected[i].Quantity);
            posted!.ElementAt(i).MeasureUnitId.Should().Be(expected[i].MeasureUnitId);
            posted!.ElementAt(i).WarehouseId.Should().Be(expected[i].PreferredWarehouseId);
        }
        posted.Should().OnlyContain(x =>
            x.TenantId == _tenantId
            && x.ProductionConfirmationId == saved!.Id
            && x.ProductionOrderId == order.Id
            && x.ReportedAt == saved!.ReportedAt);
        var pw = posted.Should().ContainSingle(x => x.MovementType == StockMovement.ReceiptType).Subject;
        pw.ProductId.Should().Be(order.ProductId);
        pw.Quantity.Should().Be(10m);
        pw.MeasureUnitId.Should().Be(order.MeasureUnitId);
        pw.WarehouseId.Should().BeNull();
        var rw = posted.Should().ContainSingle(x => x.MovementType == StockMovement.IssueType).Subject;
        rw.ProductId.Should().Be(bomProductId);
        rw.Quantity.Should().Be(20m);
        rw.MeasureUnitId.Should().Be(bomMeasureUnitId);
        rw.WarehouseId.Should().Be(warehouseId);
    }

    [Fact]
    public async Task Handle_ScrapOnlyConfirmation_PostsPreviewEquivalentLedgerLines()
    {
        // Arrange
        var order = ReleasedOrder();
        SetupOrder(order);
        var bomProductId = Guid.NewGuid();
        var bomItems = new List<BomItem>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                OperationNodeId = Guid.NewGuid(),
                ProductId = bomProductId,
                Quantity = 2m,
                QuantityType = BomQuantityType.PerUnit,
                SortIndex = 0
            }
        };
        _children.Setup(r => r.ListBomItemsForVersionAsync(order.RecipeVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bomItems);
        IReadOnlyCollection<StockMovement>? posted = null;
        _movements.Setup(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<StockMovement>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<StockMovement>, CancellationToken>((e, _) => posted = e)
            .Returns(Task.CompletedTask);

        // Act
        var request = ValidRequest(order.Id) with { GoodQuantity = 0m, ScrapQuantity = 3m };
        await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        var expected = MovementCalculator.BuildPreview(order, 0m, 1, bomItems);
        posted.Should().NotBeNull();
        posted.Should().HaveCount(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            posted!.ElementAt(i).MovementType.Should().Be(expected[i].MovementType);
            posted!.ElementAt(i).ProductId.Should().Be(expected[i].ProductId);
            posted!.ElementAt(i).Quantity.Should().Be(expected[i].Quantity);
        }
        posted.Should().ContainSingle(x => x.MovementType == StockMovement.ReceiptType && x.Quantity == 0m);
    }
}
