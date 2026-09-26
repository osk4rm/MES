using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.ProductionOrders.Close;
using AsistOff.MES.Production.Application.Features.ProductionOrders.Complete;
using AsistOff.MES.Production.Application.Features.ProductionOrders.Release;
using AsistOff.MES.Production.Application.Features.ProductionOrders.Update;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

/// <summary>
/// Optimistic-concurrency coverage for Production Order writes (issue #263):
/// matching tokens succeed, stale tokens throw <see cref="ConcurrencyConflictException"/>
/// carrying the current token, and a missing update token is a validation error.
/// </summary>
public class ProductionOrderConcurrencyTests
{
    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private readonly Mock<IProductionConfirmationsRepository> _confirmations = new();
    private readonly Mock<IRecipeVersionsRepository> _versions = new();
    private readonly Mock<IChildEntitiesRepository> _children = new();
    private readonly Mock<IMaterialReservationsRepository> _reservations = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ICurrentUserAccessor> _user = new();
    private readonly DateTime _now = new(2026, 9, 25, 8, 0, 0, DateTimeKind.Utc);

    public ProductionOrderConcurrencyTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _user.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _tenant.SetupGet(t => t.TenantId).Returns(Guid.NewGuid());
        _children.Setup(r => r.ListBomItemsForVersionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BomItem>());
        _reservations.Setup(r => r.ListForOrderAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MaterialReservation>());
        _uow.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> op, CancellationToken _) => op());
    }

    private ReleaseProductionOrderRequestHandler ReleaseSut() =>
        new(_orders.Object, _versions.Object, _children.Object, _reservations.Object,
            _guids.Object, _clock.Object, _user.Object, _tenant.Object, _uow.Object);

    private CloseProductionOrderRequestHandler CloseSut() =>
        new(_orders.Object, _confirmations.Object, _reservations.Object, _clock.Object, _uow.Object);

    private static ProductionOrder MakeOrder(ProductionOrderStatus status, uint xmin = 7) => new()
    {
        Id = Guid.NewGuid(),
        Code = "PO-001",
        ProductId = Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        RecipeVersionId = Guid.NewGuid(),
        PlannedQuantity = 100m,
        Status = status,
        Xmin = xmin
    };

    private void SetupOrder(ProductionOrder order) =>
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

    private static UpdateProductionOrderRequest UpdateRequest(ProductionOrder o, string? token) =>
        new(o.Id, o.Code, o.ProductId, o.RecipeId, o.RecipeVersionId, 25m, null, 1, null, "notes", null, token);

    [Fact]
    public async Task Update_MatchingToken_SucceedsAndExposesCurrentToken()
    {
        var order = MakeOrder(ProductionOrderStatus.Planned);
        SetupOrder(order);
        _orders.Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await new UpdateProductionOrderRequestHandler(_orders.Object)
            .Handle(UpdateRequest(order, "7"), CancellationToken.None);

        result.ConcurrencyToken.Should().Be("7");
        _orders.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_StaleToken_ThrowsConcurrencyConflictWithCurrentToken()
    {
        var order = MakeOrder(ProductionOrderStatus.Planned, xmin: 9);
        SetupOrder(order);

        var act = () => new UpdateProductionOrderRequestHandler(_orders.Object)
            .Handle(UpdateRequest(order, "7"), CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<ConcurrencyConflictException>();
        thrown.Which.CurrentToken.Should().Be("9");
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Update_MissingToken_ThrowsValidationException(string? token)
    {
        var order = MakeOrder(ProductionOrderStatus.Planned);
        SetupOrder(order);

        var act = () => new UpdateProductionOrderRequestHandler(_orders.Object)
            .Handle(UpdateRequest(order, token), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_MalformedToken_ThrowsValidationException()
    {
        var order = MakeOrder(ProductionOrderStatus.Planned);
        SetupOrder(order);

        var act = () => new UpdateProductionOrderRequestHandler(_orders.Object)
            .Handle(UpdateRequest(order, "not-a-number"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Release_StaleToken_ThrowsConcurrencyConflictWithCurrentToken()
    {
        var order = MakeOrder(ProductionOrderStatus.Planned, xmin: 11);
        SetupOrder(order);

        var act = () => ReleaseSut()
            .Handle(new ReleaseProductionOrderRequest(order.Id, "7"), CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<ConcurrencyConflictException>();
        thrown.Which.CurrentToken.Should().Be("11");
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Release_MatchingToken_ReleasesAndExposesCurrentToken()
    {
        var order = MakeOrder(ProductionOrderStatus.Planned, xmin: 11);
        SetupOrder(order);
        _versions.Setup(v => v.GetAsync(order.RecipeVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecipeVersion
            {
                Id = order.RecipeVersionId,
                RecipeId = order.RecipeId,
                VersionNumber = 1,
                Status = RecipeVersionStatus.Released
            });

        var result = await ReleaseSut()
            .Handle(new ReleaseProductionOrderRequest(order.Id, "11"), CancellationToken.None);

        result.Status.Should().Be(ProductionOrderStatus.Released);
        result.ConcurrencyToken.Should().Be("11");
        _orders.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Complete_StaleToken_ThrowsConcurrencyConflictWithCurrentToken()
    {
        var order = MakeOrder(ProductionOrderStatus.InProgress, xmin: 13);
        SetupOrder(order);

        var act = () => new CompleteProductionOrderRequestHandler(_orders.Object, _confirmations.Object, _clock.Object)
            .Handle(new CompleteProductionOrderRequest(order.Id, "7"), CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<ConcurrencyConflictException>();
        thrown.Which.CurrentToken.Should().Be("13");
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Complete_MatchingToken_CompletesAndExposesCurrentToken()
    {
        var order = MakeOrder(ProductionOrderStatus.InProgress, xmin: 13);
        SetupOrder(order);
        _confirmations.Setup(r => r.GetTotalsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((60m, 5m, 3));

        var result = await new CompleteProductionOrderRequestHandler(_orders.Object, _confirmations.Object, _clock.Object)
            .Handle(new CompleteProductionOrderRequest(order.Id, "13"), CancellationToken.None);

        result.Status.Should().Be(ProductionOrderStatus.Completed);
        result.ConcurrencyToken.Should().Be("13");
        _orders.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Close_StaleToken_ThrowsConcurrencyConflictWithCurrentToken()
    {
        var order = MakeOrder(ProductionOrderStatus.Completed, xmin: 17);
        SetupOrder(order);

        var act = () => CloseSut()
            .Handle(new CloseProductionOrderRequest(order.Id, "7"), CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<ConcurrencyConflictException>();
        thrown.Which.CurrentToken.Should().Be("17");
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Close_MatchingToken_ClosesAndExposesCurrentToken()
    {
        var order = MakeOrder(ProductionOrderStatus.Completed, xmin: 17);
        SetupOrder(order);
        _confirmations.Setup(r => r.GetTotalsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((60m, 5m, 3));

        var result = await CloseSut()
            .Handle(new CloseProductionOrderRequest(order.Id, "17"), CancellationToken.None);

        result.Status.Should().Be(ProductionOrderStatus.Closed);
        result.ConcurrencyToken.Should().Be("17");
        _orders.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
    }
}
