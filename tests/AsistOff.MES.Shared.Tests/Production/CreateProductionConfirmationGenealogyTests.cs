using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.LotGenealogy.Browse;
using AsistOff.MES.Production.Application.Features.ProductionConfirmations;
using AsistOff.MES.Production.Application.Features.ProductionConfirmations.Create;
using AsistOff.MES.Production.Application.Features.ProductionConfirmations.Delete;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using LinqKit;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

/// <summary>
/// Unit tests for automatic lot genealogy derivation on confirmation
/// (issue #207): one <see cref="LotGenealogyEdge"/> per consumed lot entry,
/// validation of lot references, and explicit edge cleanup on delete.
/// </summary>
public class CreateProductionConfirmationGenealogyTests
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
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IMaterialReservationsRepository> _reservations = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    public CreateProductionConfirmationGenealogyTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
        _children.Setup(r => r.ListBomItemsForVersionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BomItem>());
        _reservations.Setup(r => r.ListForOrderAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MaterialReservation>());
        _uow.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> op, CancellationToken _) => op());
        _uow.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<ProductionConfirmationResponse>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task<ProductionConfirmationResponse>> op, CancellationToken _) => op());
    }

    private CreateProductionConfirmationRequestHandler CreateSut() =>
        new(_confirmations.Object, _orders.Object, _children.Object, _movements.Object,
            _lots.Object, _edges.Object, _guids.Object, _clock.Object, _tenant.Object, _uow.Object, _reservations.Object);

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

    private static CreateProductionConfirmationRequest ValidRequest(
        Guid orderId,
        Guid? producedLotId,
        params ConsumedLotEntry[] consumed) => new(
        orderId,
        Guid.NewGuid(),
        Guid.NewGuid(),
        new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc),
        10m,
        0m,
        null,
        producedLotId,
        consumed);

    [Fact]
    public async Task Handle_WithProducedAndTwoConsumedLots_PostsTwoEdgesWithOrderConfirmationMachineOperatorAndTimestamp()
    {
        // Arrange
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var producedId = Guid.NewGuid();
        var consumedA = Guid.NewGuid();
        var consumedB = Guid.NewGuid();
        _lots.Setup(r => r.GetAsync(producedId, It.IsAny<CancellationToken>())).ReturnsAsync(NewLot(producedId));
        _lots.Setup(r => r.GetAsync(consumedA, It.IsAny<CancellationToken>())).ReturnsAsync(NewLot(consumedA));
        _lots.Setup(r => r.GetAsync(consumedB, It.IsAny<CancellationToken>())).ReturnsAsync(NewLot(consumedB));
        ProductionConfirmation? saved = null;
        _confirmations.Setup(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()))
            .Callback<ProductionConfirmation, CancellationToken>((e, _) => saved = e)
            .ReturnsAsync((ProductionConfirmation e, CancellationToken _) => e);
        var posted = new List<LotGenealogyEdge>();
        _edges.Setup(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()))
            .Callback<LotGenealogyEdge, CancellationToken>((e, _) => posted.Add(e))
            .ReturnsAsync((LotGenealogyEdge e, CancellationToken _) => e);
        var request = ValidRequest(order.Id, producedId,
            new ConsumedLotEntry(consumedA, 5m),
            new ConsumedLotEntry(consumedB, 7m));

        // Act
        await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        saved.Should().NotBeNull();
        posted.Should().HaveCount(2);
        posted.Select(e => e.ConsumedLotId).Should().BeEquivalentTo([consumedA, consumedB]);
        posted.Select(e => e.ConsumedQuantity).Should().BeEquivalentTo([5m, 7m]);
        posted.Should().OnlyContain(e =>
            e.ProducedLotId == producedId
            && e.ProductionOrderId == order.Id
            && e.ProductionConfirmationId == saved!.Id
            && e.MachineId == saved!.MachineId
            && e.ReportedByOperatorId == saved!.ReportedByOperatorId
            && e.OccurredAt == saved!.ReportedAt
            && e.TenantId == _tenantId);
    }

    [Fact]
    public async Task Handle_WithoutLotReferences_PostsNoEdges()
    {
        // Arrange
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var request = new CreateProductionConfirmationRequest(
            order.Id, Guid.NewGuid(), null,
            new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc), 10m, 0m, null);

        // Act
        await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        _edges.Verify(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()), Times.Never);
        _lots.Verify(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SelfLink_ThrowsValidationException()
    {
        // Arrange
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var lotId = Guid.NewGuid();
        var request = ValidRequest(order.Id, lotId, new ConsumedLotEntry(lotId, 5m));

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _edges.Verify(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2.5)]
    public async Task Handle_NonPositiveConsumedQuantity_ThrowsValidationException(decimal quantity)
    {
        // Arrange
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var request = ValidRequest(order.Id, Guid.NewGuid(), new ConsumedLotEntry(Guid.NewGuid(), quantity));

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _edges.Verify(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ConsumedLotsWithoutProducedLot_ThrowsValidationException()
    {
        // Arrange
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var request = ValidRequest(order.Id, null, new ConsumedLotEntry(Guid.NewGuid(), 5m));

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_UnknownProducedLot_ThrowsNotFoundException()
    {
        // Arrange
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var producedId = Guid.NewGuid();
        var consumedId = Guid.NewGuid();
        _lots.Setup(r => r.GetAsync(producedId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lot?)null);
        _lots.Setup(r => r.GetAsync(consumedId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewLot(consumedId));
        var request = ValidRequest(order.Id, producedId, new ConsumedLotEntry(consumedId, 5m));

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _confirmations.Verify(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownConsumedLot_ThrowsNotFoundException()
    {
        // Arrange
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var producedId = Guid.NewGuid();
        var consumedId = Guid.NewGuid();
        _lots.Setup(r => r.GetAsync(producedId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewLot(producedId));
        _lots.Setup(r => r.GetAsync(consumedId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lot?)null);
        var request = ValidRequest(order.Id, producedId, new ConsumedLotEntry(consumedId, 5m));

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _confirmations.Verify(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CrossTenantLot_ResolvesToNotFound()
    {
        // Arrange — cross-tenant rows are hidden by the global query filter,
        // so the repository returns null and the handler maps it to 404.
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var producedId = Guid.NewGuid();
        _lots.Setup(r => r.GetAsync(producedId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lot?)null);
        var request = ValidRequest(order.Id, producedId, new ConsumedLotEntry(Guid.NewGuid(), 5m));

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _edges.Verify(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CrossTenantConsumedLot_ResolvesToNotFound()
    {
        // Arrange — same hidden-row path as the produced lot: a consumed lot
        // from another tenant is invisible behind the global query filter,
        // so GetAsync returns null and the handler maps it to 404 without
        // persisting the confirmation (issue #221 remainder).
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var producedId = Guid.NewGuid();
        var consumedId = Guid.NewGuid();
        _lots.Setup(r => r.GetAsync(producedId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewLot(producedId));
        _lots.Setup(r => r.GetAsync(consumedId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lot?)null);
        var request = ValidRequest(order.Id, producedId, new ConsumedLotEntry(consumedId, 5m));

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _confirmations.Verify(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()), Times.Never);
        _edges.Verify(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DeleteConfirmation_DeletesAutoPostedEdgesInCode()
    {
        // Arrange
        var confirmation = new ProductionConfirmation
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ProductionOrderId = Guid.NewGuid(),
            MachineId = Guid.NewGuid(),
            ReportedAt = new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc),
            GoodQuantity = 10m,
            ScrapQuantity = 0m,
            CreatedAt = _now
        };
        var order = ReleasedOrder();
        order.Id = confirmation.ProductionOrderId;
        order.Status = ProductionOrderStatus.InProgress;
        _confirmations.Setup(r => r.GetAsync(confirmation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(confirmation);
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var deleteSut = new DeleteProductionConfirmationRequestHandler(
            _confirmations.Object, _orders.Object, _edges.Object);

        // Act
        await deleteSut.Handle(new DeleteProductionConfirmationRequest(confirmation.Id), CancellationToken.None);

        // Assert
        _edges.Verify(r => r.DeleteByConfirmationAsync(confirmation.Id, It.IsAny<CancellationToken>()), Times.Once);
        _confirmations.Verify(r => r.DeleteAsync(confirmation.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_BrowseByProductionConfirmationId_FiltersToMatchingEdgesOnly()
    {
        // Arrange
        var confirmationId = Guid.NewGuid();
        var otherConfirmationId = Guid.NewGuid();
        var matching = new LotGenealogyEdge
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ConsumedLotId = Guid.NewGuid(),
            ProducedLotId = Guid.NewGuid(),
            ProductionOrderId = Guid.NewGuid(),
            ProductionConfirmationId = confirmationId,
            MachineId = Guid.NewGuid(),
            ConsumedQuantity = 5m,
            OccurredAt = _now,
            CreatedAt = _now
        };
        var other = new LotGenealogyEdge
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ConsumedLotId = Guid.NewGuid(),
            ProducedLotId = Guid.NewGuid(),
            ProductionOrderId = Guid.NewGuid(),
            ProductionConfirmationId = otherConfirmationId,
            MachineId = Guid.NewGuid(),
            ConsumedQuantity = 3m,
            OccurredAt = _now,
            CreatedAt = _now
        };
        ExpressionStarter<LotGenealogyEdge>? capturedCount = null;
        Paginator<LotGenealogyEdge>? capturedPage = null;
        var edgesRepo = new Mock<ILotGenealogyEdgesRepository>();
        edgesRepo.Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<LotGenealogyEdge>>(), It.IsAny<CancellationToken>()))
            .Callback<ExpressionStarter<LotGenealogyEdge>, CancellationToken>((p, _) => capturedCount = p)
            .ReturnsAsync(1);
        edgesRepo.Setup(r => r.BrowseAsync(It.IsAny<Paginator<LotGenealogyEdge>>(), It.IsAny<CancellationToken>()))
            .Callback<Paginator<LotGenealogyEdge>, CancellationToken>((p, _) => capturedPage = p)
            .ReturnsAsync(new List<LotGenealogyEdge> { matching });
        var sut = new BrowseLotGenealogyEdgesRequestHandler(edgesRepo.Object);
        var request = new BrowseLotGenealogyEdgesRequest { ProductionConfirmationId = confirmationId };

        // Act
        var result = await sut.Handle(request, CancellationToken.None);

        // Assert
        result.Items.Should().ContainSingle(i => i.ProductionConfirmationId == confirmationId);
        capturedCount.Should().NotBeNull();
        capturedPage.Should().NotBeNull();
        var predicate = capturedCount!.Compile();
        predicate(matching).Should().BeTrue();
        predicate(other).Should().BeFalse();
    }
}
