using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
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

/// <summary>
/// Proves the operator confirmation fan-out is atomic (issue #265): the
/// confirmation row, RW/PW movements, genealogy edges and the order status
/// flip commit together, and any mid-fan-out failure rolls everything back.
/// Real end-to-end rollback is proven by the endpoint integration suite; here
/// the mocked <see cref="IUnitOfWork"/> verifies the transaction boundary
/// (commit on success, rollback on failure, no transaction on validation
/// rejection).
/// </summary>
public class CreateProductionConfirmationAtomicityTests
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
    private readonly Mock<IDatabaseTransaction> _transaction = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    public CreateProductionConfirmationAtomicityTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
        _unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transaction.Object);
        _children.Setup(r => r.ListBomItemsForVersionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BomItem>());
    }

    private CreateProductionConfirmationRequestHandler CreateSut() =>
        new(_confirmations.Object, _orders.Object, _children.Object, _movements.Object,
            _lots.Object, _edges.Object, _guids.Object, _clock.Object, _tenant.Object, _unitOfWork.Object);

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

    private static Lot NewLot(Guid id) => new()
    {
        Id = id,
        TenantId = Guid.NewGuid(),
        Code = $"LOT-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
        ProductId = Guid.NewGuid(),
        MeasureUnitId = Guid.NewGuid(),
        Quantity = 100m,
        Status = LotStatus.Available,
        CreatedAt = new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc)
    };

    private CreateProductionConfirmationRequest ValidRequest(Guid orderId) => new(
        orderId,
        Guid.NewGuid(),
        null,
        new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc),
        10m,
        2m,
        null);

    [Fact]
    public async Task Handle_HappyPath_CommitsTransactionAndPersistsAllWritesTogether()
    {
        // Arrange
        var order = ReleasedOrder();
        order.MeasureUnitId = Guid.NewGuid();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var producedId = Guid.NewGuid();
        var consumedId = Guid.NewGuid();
        _lots.Setup(r => r.GetAsync(producedId, It.IsAny<CancellationToken>())).ReturnsAsync(NewLot(producedId));
        _lots.Setup(r => r.GetAsync(consumedId, It.IsAny<CancellationToken>())).ReturnsAsync(NewLot(consumedId));
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
        ProductionConfirmation? saved = null;
        _confirmations.Setup(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()))
            .Callback<ProductionConfirmation, CancellationToken>((e, _) => saved = e)
            .ReturnsAsync((ProductionConfirmation e, CancellationToken _) => e);
        IReadOnlyCollection<StockMovement>? posted = null;
        _movements.Setup(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<StockMovement>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<StockMovement>, CancellationToken>((e, _) => posted = e)
            .Returns(Task.CompletedTask);
        var request = new CreateProductionConfirmationRequest(
            order.Id, Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc), 10m, 0m, null,
            producedId, new[] { new ConsumedLotEntry(consumedId, 5m) });

        // Act
        await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        saved.Should().NotBeNull();
        _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _confirmations.Verify(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()), Times.Once);
        _movements.Verify(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<StockMovement>>(), It.IsAny<CancellationToken>()), Times.Once);
        _edges.Verify(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()), Times.Once);
        order.Status.Should().Be(ProductionOrderStatus.InProgress);
        _orders.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        _transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _transaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);

        var expected = MovementCalculator.BuildPreview(order, 10m, 1, bomItems);
        posted.Should().NotBeNull();
        posted.Should().HaveCount(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            posted!.ElementAt(i).MovementType.Should().Be(expected[i].MovementType);
            posted!.ElementAt(i).ProductId.Should().Be(expected[i].ProductId);
            posted!.ElementAt(i).Quantity.Should().Be(expected[i].Quantity);
        }
    }

    [Fact]
    public async Task Handle_MovementFailure_RollsBackAndPropagatesWithoutOrderFlip()
    {
        // Arrange
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _movements.Setup(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<StockMovement>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("movement ledger unavailable"));

        // Act
        var act = () => CreateSut().Handle(ValidRequest(order.Id), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _confirmations.Verify(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()), Times.Once);
        _transaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _edges.Verify(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()), Times.Never);
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EdgeFailure_RollsBackAndPropagatesWithoutOrderFlip()
    {
        // Arrange
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var producedId = Guid.NewGuid();
        var consumedId = Guid.NewGuid();
        _lots.Setup(r => r.GetAsync(producedId, It.IsAny<CancellationToken>())).ReturnsAsync(NewLot(producedId));
        _lots.Setup(r => r.GetAsync(consumedId, It.IsAny<CancellationToken>())).ReturnsAsync(NewLot(consumedId));
        _edges.Setup(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("genealogy unavailable"));
        var request = new CreateProductionConfirmationRequest(
            order.Id, Guid.NewGuid(), null,
            new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc), 10m, 0m, null,
            producedId, new[] { new ConsumedLotEntry(consumedId, 5m) });

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _transaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ClosedOrder_WritesNothingAndOpensNoTransaction()
    {
        // Arrange
        var order = ReleasedOrder();
        order.Status = ProductionOrderStatus.Closed;
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act
        var act = () => CreateSut().Handle(ValidRequest(order.Id), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
        _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _confirmations.Verify(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()), Times.Never);
        _movements.Verify(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<StockMovement>>(), It.IsAny<CancellationToken>()), Times.Never);
        _edges.Verify(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()), Times.Never);
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownOrder_WritesNothingAndOpensNoTransaction()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _orders.Setup(r => r.GetAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductionOrder?)null);

        // Act
        var act = () => CreateSut().Handle(ValidRequest(orderId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _confirmations.Verify(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ZeroQuantityValidation_WritesNothingAndOpensNoTransaction()
    {
        // Arrange
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var request = ValidRequest(order.Id) with { GoodQuantity = 0m, ScrapQuantity = 0m };

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _confirmations.Verify(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
