using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Application.Features.ProductionConfirmations.Create;
using AsistOff.MES.Production.Application.Features.ProductionOrders.Close;
using AsistOff.MES.Production.Application.Features.ProductionOrders.Complete;
using AsistOff.MES.Production.Application.Features.ProductionOrders.Hold;
using AsistOff.MES.Production.Application.Features.ProductionOrders.Release;
using AsistOff.MES.Production.Application.Features.ProductionOrders.Resume;
using AsistOff.MES.Production.Application.Features.Schedule;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Users.Core.Rbac;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class HoldResumeProductionOrderRequestHandlerTests
{
    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly DateTime _now = new(2026, 9, 24, 14, 0, 0, DateTimeKind.Utc);

    public HoldResumeProductionOrderRequestHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
    }

    private HoldProductionOrderRequestHandler CreateHoldSut() =>
        new(_orders.Object, _clock.Object);

    private ResumeProductionOrderRequestHandler CreateResumeSut() =>
        new(_orders.Object, _clock.Object);

    private static ProductionOrder MakeOrder(ProductionOrderStatus status, uint xmin = 0) => new()
    {
        Id = Guid.NewGuid(),
        Code = "PO-001",
        ProductId = Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        RecipeVersionId = Guid.NewGuid(),
        PlannedQuantity = 100m,
        Status = status,
        Xmin = xmin,
        CreatedAt = new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc)
    };

    private static ProductionOrder MakeHeldOrder(ProductionOrderStatus beforeHold) => new()
    {
        Id = Guid.NewGuid(),
        Code = "PO-HELD",
        ProductId = Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        RecipeVersionId = Guid.NewGuid(),
        PlannedQuantity = 100m,
        Status = ProductionOrderStatus.OnHold,
        StatusBeforeHold = beforeHold,
        HeldAtUtc = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc),
        HoldReason = "MATERIAL-SHORTAGE",
        CreatedAt = new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc)
    };

    private void SetupOrder(ProductionOrder order) =>
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

    [Fact]
    public void HoldRequest_ImplementsTenantRequest_AndNotAnonymous_AndRequiresProductionWrite()
    {
        typeof(ITenantRequest<ProductionOrderResponse>)
            .IsAssignableFrom(typeof(HoldProductionOrderRequest)).Should().BeTrue();
        typeof(IAllowAnonymousRequest).IsAssignableFrom(typeof(HoldProductionOrderRequest)).Should().BeFalse();
        typeof(HoldProductionOrderRequest)
            .GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true)
            .OfType<RequirePermissionAttribute>()
            .Select(a => a.Permission)
            .Should().Contain(RbacDefaults.ProductionWrite);
    }

    [Fact]
    public void ResumeRequest_ImplementsTenantRequest_AndNotAnonymous_AndRequiresProductionWrite()
    {
        typeof(ITenantRequest<ProductionOrderResponse>)
            .IsAssignableFrom(typeof(ResumeProductionOrderRequest)).Should().BeTrue();
        typeof(IAllowAnonymousRequest).IsAssignableFrom(typeof(ResumeProductionOrderRequest)).Should().BeFalse();
        typeof(ResumeProductionOrderRequest)
            .GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true)
            .OfType<RequirePermissionAttribute>()
            .Select(a => a.Permission)
            .Should().Contain(RbacDefaults.ProductionWrite);
    }

    [Theory]
    [InlineData(ProductionOrderStatus.Released)]
    [InlineData(ProductionOrderStatus.InProgress)]
    public async Task Handle_HoldOpenOrder_MovesToOnHoldAndRecordsHoldState(ProductionOrderStatus status)
    {
        var order = MakeOrder(status);
        SetupOrder(order);

        var result = await CreateHoldSut().Handle(
            new HoldProductionOrderRequest(order.Id, null, "MATERIAL-SHORTAGE"), CancellationToken.None);

        order.Status.Should().Be(ProductionOrderStatus.OnHold);
        order.StatusBeforeHold.Should().Be(status);
        order.HeldAtUtc.Should().Be(_now);
        order.HoldReason.Should().Be("MATERIAL-SHORTAGE");
        _orders.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        result.Status.Should().Be(ProductionOrderStatus.OnHold);
        result.StatusBeforeHold.Should().Be(status);
        result.HeldAtUtc.Should().Be(_now);
        result.HoldReason.Should().Be("MATERIAL-SHORTAGE");
    }

    [Fact]
    public async Task Handle_HoldWithoutReason_StoresNullReason()
    {
        var order = MakeOrder(ProductionOrderStatus.Released);
        SetupOrder(order);

        var result = await CreateHoldSut().Handle(
            new HoldProductionOrderRequest(order.Id), CancellationToken.None);

        order.Status.Should().Be(ProductionOrderStatus.OnHold);
        order.HoldReason.Should().BeNull();
        result.HoldReason.Should().BeNull();
    }

    [Fact]
    public async Task Handle_HoldReasonTooLong_ThrowsValidationException()
    {
        var order = MakeOrder(ProductionOrderStatus.Released);
        SetupOrder(order);

        var act = () => CreateHoldSut().Handle(
            new HoldProductionOrderRequest(order.Id, null, new string('R', 501)), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(ProductionOrderStatus.Planned)]
    [InlineData(ProductionOrderStatus.Completed)]
    [InlineData(ProductionOrderStatus.Closed)]
    [InlineData(ProductionOrderStatus.OnHold)]
    public async Task Handle_HoldWrongStatus_ThrowsConflictException(ProductionOrderStatus status)
    {
        var order = status == ProductionOrderStatus.OnHold
            ? MakeHeldOrder(ProductionOrderStatus.Released)
            : MakeOrder(status);
        SetupOrder(order);

        var act = () => CreateHoldSut().Handle(
            new HoldProductionOrderRequest(order.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_HoldUnknownOrder_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        _orders.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductionOrder?)null);

        var act = () => CreateHoldSut().Handle(
            new HoldProductionOrderRequest(id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_HoldStaleToken_ThrowsConcurrencyConflictWithCurrentToken()
    {
        var order = MakeOrder(ProductionOrderStatus.Released, xmin: 9);
        SetupOrder(order);

        var act = () => CreateHoldSut().Handle(
            new HoldProductionOrderRequest(order.Id, "3"), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ConcurrencyConflictException>();
        ex.Which.CurrentToken.Should().Be("9");
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(ProductionOrderStatus.Released)]
    [InlineData(ProductionOrderStatus.InProgress)]
    public async Task Handle_ResumeHeldOrder_RestoresPreviousStatusAndClearsHoldState(ProductionOrderStatus beforeHold)
    {
        var order = MakeHeldOrder(beforeHold);
        SetupOrder(order);

        var result = await CreateResumeSut().Handle(
            new ResumeProductionOrderRequest(order.Id), CancellationToken.None);

        order.Status.Should().Be(beforeHold);
        order.StatusBeforeHold.Should().BeNull();
        order.HeldAtUtc.Should().BeNull();
        order.HoldReason.Should().BeNull();
        _orders.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        result.Status.Should().Be(beforeHold);
        result.HeldAtUtc.Should().BeNull();
        result.HoldReason.Should().BeNull();
    }

    [Theory]
    [InlineData(ProductionOrderStatus.Planned)]
    [InlineData(ProductionOrderStatus.Released)]
    [InlineData(ProductionOrderStatus.InProgress)]
    [InlineData(ProductionOrderStatus.Completed)]
    [InlineData(ProductionOrderStatus.Closed)]
    public async Task Handle_ResumeNotHeldOrder_ThrowsConflictException(ProductionOrderStatus status)
    {
        var order = MakeOrder(status);
        SetupOrder(order);

        var act = () => CreateResumeSut().Handle(
            new ResumeProductionOrderRequest(order.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ResumeUnknownOrder_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        _orders.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductionOrder?)null);

        var act = () => CreateResumeSut().Handle(
            new ResumeProductionOrderRequest(id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ResumeStaleToken_ThrowsConcurrencyConflictWithCurrentToken()
    {
        var order = MakeHeldOrder(ProductionOrderStatus.InProgress);
        order.Xmin = 5;
        SetupOrder(order);

        var act = () => CreateResumeSut().Handle(
            new ResumeProductionOrderRequest(order.Id, "1"), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ConcurrencyConflictException>();
        ex.Which.CurrentToken.Should().Be("5");
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CreateConfirmationOnHeldOrder_ThrowsValidationException()
    {
        var order = MakeHeldOrder(ProductionOrderStatus.Released);
        var orders = new Mock<IProductionOrdersRepository>();
        orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var sut = new CreateProductionConfirmationRequestHandler(
            Mock.Of<IProductionConfirmationsRepository>(),
            orders.Object,
            Mock.Of<IChildEntitiesRepository>(),
            Mock.Of<IStockMovementsRepository>(),
            Mock.Of<ILotsRepository>(),
            Mock.Of<ILotGenealogyEdgesRepository>(),
            Mock.Of<IGuidProvider>(),
            Mock.Of<IDateTimeProvider>(),
            Mock.Of<ITenantContext>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IMaterialReservationsRepository>());

        var act = () => sut.Handle(
            new CreateProductionConfirmationRequest(
                order.Id, Guid.NewGuid(), null, _now, 10m, 0m, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_CompleteHeldOrder_ThrowsValidationException()
    {
        var order = MakeHeldOrder(ProductionOrderStatus.InProgress);
        SetupOrder(order);
        var sut = new CompleteProductionOrderRequestHandler(
            _orders.Object, Mock.Of<IProductionConfirmationsRepository>(), _clock.Object);

        var act = () => sut.Handle(
            new CompleteProductionOrderRequest(order.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CloseHeldOrder_ThrowsValidationException()
    {
        var order = MakeHeldOrder(ProductionOrderStatus.Released);
        SetupOrder(order);
        var sut = new CloseProductionOrderRequestHandler(
            _orders.Object,
            Mock.Of<IProductionConfirmationsRepository>(),
            Mock.Of<IMaterialReservationsRepository>(),
            _clock.Object,
            Mock.Of<IUnitOfWork>());

        var act = () => sut.Handle(
            new CloseProductionOrderRequest(order.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReleaseHeldOrder_ThrowsValidationException()
    {
        var order = MakeHeldOrder(ProductionOrderStatus.Released);
        SetupOrder(order);
        var sut = new ReleaseProductionOrderRequestHandler(
            _orders.Object,
            Mock.Of<IRecipeVersionsRepository>(),
            Mock.Of<IChildEntitiesRepository>(),
            Mock.Of<IMaterialReservationsRepository>(),
            Mock.Of<IGuidProvider>(),
            Mock.Of<IDateTimeProvider>(),
            Mock.Of<ICurrentUserAccessor>(),
            Mock.Of<ITenantContext>(),
            Mock.Of<IUnitOfWork>());

        var act = () => sut.Handle(
            new ReleaseProductionOrderRequest(order.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RescheduleHeldOrder_ThrowsValidationException()
    {
        var order = MakeHeldOrder(ProductionOrderStatus.Released);
        order.Xmin = 7;
        SetupOrder(order);
        var sut = new RescheduleGanttSegmentRequestHandler(
            _orders.Object,
            Mock.Of<IOperationNodesRepository>(),
            Mock.Of<IScheduledOperationsRepository>(),
            Mock.Of<IMachinesRepository>(),
            Mock.Of<IShiftsRepository>(),
            Mock.Of<IOperatorShiftAssignmentsRepository>(),
            Mock.Of<IGuidProvider>(),
            Mock.Of<ITenantContext>());

        var act = () => sut.Handle(
            new RescheduleGanttSegmentRequest(
                Guid.NewGuid(), order.Id,
                new DateTime(2026, 9, 25, 8, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc),
                Guid.NewGuid(), "7", false, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }
}
