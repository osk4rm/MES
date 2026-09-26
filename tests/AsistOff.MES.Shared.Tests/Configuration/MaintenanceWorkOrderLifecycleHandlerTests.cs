using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Cancel;
using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Complete;
using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Start;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class MaintenanceWorkOrderLifecycleHandlerTests
{
    private readonly Mock<IMaintenanceWorkOrdersRepository> _repository = new();
    private readonly Mock<IMaintenancePlansRepository> _plansRepository = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
    private readonly Guid _orderId = Guid.NewGuid();

    public MaintenanceWorkOrderLifecycleHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
    }

    private static MaintenanceWorkOrder Order(MaintenanceWorkOrderStatus status) => new()
    {
        Id = Guid.NewGuid(),
        Code = "WO-1",
        Title = "Fix spindle",
        MachineId = Guid.NewGuid(),
        Priority = MaintenanceWorkOrderPriority.High,
        Status = status,
        ReportedAt = new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc)
    };

    private CompleteMaintenanceWorkOrderRequestHandler CompleteHandler()
        => new(_repository.Object, _plansRepository.Object, _clock.Object);

    private void SetupGet(MaintenanceWorkOrder? order)
    {
        _repository
            .Setup(r => r.GetByIdAsync(_orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
    }

    [Fact]
    public async Task Start_OpenOrder_MovesToInProgressAndSetsStartedAt()
    {
        var order = Order(MaintenanceWorkOrderStatus.Open);
        SetupGet(order);

        var result = await new StartMaintenanceWorkOrderRequestHandler(_repository.Object, _clock.Object)
            .Handle(new StartMaintenanceWorkOrderRequest(_orderId), CancellationToken.None);

        order.Status.Should().Be(MaintenanceWorkOrderStatus.InProgress);
        order.StartedAt.Should().Be(_now);
        result.Status.Should().Be(MaintenanceWorkOrderStatus.InProgress);
        _repository.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(MaintenanceWorkOrderStatus.InProgress)]
    [InlineData(MaintenanceWorkOrderStatus.Done)]
    [InlineData(MaintenanceWorkOrderStatus.Cancelled)]
    public async Task Start_NonOpenOrder_ThrowsValidationException(MaintenanceWorkOrderStatus status)
    {
        SetupGet(Order(status));

        var act = () => new StartMaintenanceWorkOrderRequestHandler(_repository.Object, _clock.Object)
            .Handle(new StartMaintenanceWorkOrderRequest(_orderId), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Start_UnknownId_ThrowsNotFoundException()
    {
        SetupGet(null);

        var act = () => new StartMaintenanceWorkOrderRequestHandler(_repository.Object, _clock.Object)
            .Handle(new StartMaintenanceWorkOrderRequest(_orderId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData(MaintenanceWorkOrderStatus.Open)]
    [InlineData(MaintenanceWorkOrderStatus.InProgress)]
    public async Task Complete_OpenOrInProgressOrder_MovesToDoneWithNotes(MaintenanceWorkOrderStatus status)
    {
        var order = Order(status);
        SetupGet(order);

        var result = await CompleteHandler()
            .Handle(new CompleteMaintenanceWorkOrderRequest(_orderId, "Replaced bearing"), CancellationToken.None);

        order.Status.Should().Be(MaintenanceWorkOrderStatus.Done);
        order.ResolutionNotes.Should().Be("Replaced bearing");
        order.CompletedAt.Should().Be(_now);
        result.Status.Should().Be(MaintenanceWorkOrderStatus.Done);
        _repository.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Complete_EmptyResolutionNotes_ThrowsValidationException()
    {
        SetupGet(Order(MaintenanceWorkOrderStatus.Open));

        var act = () => CompleteHandler()
            .Handle(new CompleteMaintenanceWorkOrderRequest(_orderId, "  "), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Theory]
    [InlineData(MaintenanceWorkOrderStatus.Done)]
    [InlineData(MaintenanceWorkOrderStatus.Cancelled)]
    public async Task Complete_DoneOrCancelledOrder_ThrowsValidationException(MaintenanceWorkOrderStatus status)
    {
        SetupGet(Order(status));

        var act = () => CompleteHandler()
            .Handle(new CompleteMaintenanceWorkOrderRequest(_orderId, "Notes"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Theory]
    [InlineData(MaintenanceWorkOrderStatus.Open)]
    [InlineData(MaintenanceWorkOrderStatus.InProgress)]
    public async Task Cancel_OpenOrInProgressOrder_MovesToCancelled(MaintenanceWorkOrderStatus status)
    {
        var order = Order(status);
        SetupGet(order);

        var result = await new CancelMaintenanceWorkOrderRequestHandler(_repository.Object)
            .Handle(new CancelMaintenanceWorkOrderRequest(_orderId), CancellationToken.None);

        order.Status.Should().Be(MaintenanceWorkOrderStatus.Cancelled);
        result.Status.Should().Be(MaintenanceWorkOrderStatus.Cancelled);
        _repository.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(MaintenanceWorkOrderStatus.Done)]
    [InlineData(MaintenanceWorkOrderStatus.Cancelled)]
    public async Task Cancel_DoneOrCancelledOrder_ThrowsValidationException(MaintenanceWorkOrderStatus status)
    {
        SetupGet(Order(status));

        var act = () => new CancelMaintenanceWorkOrderRequestHandler(_repository.Object)
            .Handle(new CancelMaintenanceWorkOrderRequest(_orderId), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Cancel_UnknownId_ThrowsNotFoundException()
    {
        SetupGet(null);

        var act = () => new CancelMaintenanceWorkOrderRequestHandler(_repository.Object)
            .Handle(new CancelMaintenanceWorkOrderRequest(_orderId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
