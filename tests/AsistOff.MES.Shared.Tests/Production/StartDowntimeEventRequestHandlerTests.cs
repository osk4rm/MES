using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.DowntimeEvents.Start;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class StartDowntimeEventRequestHandlerTests
{
    private readonly Mock<IDowntimeEventsRepository> _repository = new();
    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _eventId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    public StartDowntimeEventRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(_eventId);
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
        _repository.Setup(r => r.HasOpenEventAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private StartDowntimeEventRequestHandler CreateSut() =>
        new(_repository.Object, _orders.Object, _guids.Object, _clock.Object, _tenant.Object);

    private static StartDowntimeEventRequest ValidRequest(DateTime? startedAt = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), startedAt ?? new DateTime(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc), "notes", null);

    [Fact]
    public async Task Handle_MissingMachineId_ThrowsValidationException()
    {
        var request = ValidRequest() with { MachineId = Guid.Empty };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_MissingReasonCodeId_ThrowsValidationException()
    {
        var request = ValidRequest() with { ReasonCodeId = Guid.Empty };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_MissingStartedAt_ThrowsValidationException()
    {
        var request = ValidRequest() with { StartedAt = default };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_StartedAtInFuture_ThrowsValidationException()
    {
        var request = ValidRequest(_now.AddHours(1));

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_SecondOpenEventOnSameWorkCenter_ThrowsConflictException()
    {
        _repository.Setup(r => r.HasOpenEventAsync(It.IsAny<Guid>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => CreateSut().Handle(ValidRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_ReleasedOrderLink_StoresLink()
    {
        var order = OrderWithStatus(ProductionOrderStatus.Released);
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        DowntimeEvent? saved = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<DowntimeEvent>(), It.IsAny<CancellationToken>()))
            .Callback<DowntimeEvent, CancellationToken>((e, _) => saved = e)
            .ReturnsAsync((DowntimeEvent e, CancellationToken _) => e);

        var result = await CreateSut().Handle(ValidRequest() with { ProductionOrderId = order.Id }, CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.ProductionOrderId.Should().Be(order.Id);
        result.ProductionOrderId.Should().Be(order.Id);
    }

    [Theory]
    [InlineData(ProductionOrderStatus.Planned)]
    [InlineData(ProductionOrderStatus.Completed)]
    [InlineData(ProductionOrderStatus.Closed)]
    public async Task Handle_WrongStatusOrderLink_ThrowsValidationException(ProductionOrderStatus status)
    {
        var order = OrderWithStatus(status);
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var act = () => CreateSut().Handle(ValidRequest() with { ProductionOrderId = order.Id }, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_UnknownOrderLink_ThrowsNotFoundException()
    {
        // The tenant global query filter hides cross-tenant orders, so both
        // unknown and cross-tenant ids surface as null here.
        _orders.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductionOrder?)null);

        var act = () => CreateSut().Handle(ValidRequest() with { ProductionOrderId = Guid.NewGuid() }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_HappyPath_SetsTenantIdAndLeavesEventOpen()
    {
        DowntimeEvent? saved = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<DowntimeEvent>(), It.IsAny<CancellationToken>()))
            .Callback<DowntimeEvent, CancellationToken>((e, _) => saved = e)
            .ReturnsAsync((DowntimeEvent e, CancellationToken _) => e);

        var result = await CreateSut().Handle(ValidRequest(), CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.TenantId.Should().Be(_tenantId);
        saved.EndedAt.Should().BeNull();
        saved.ProductionOrderId.Should().BeNull();
        result.Status.Should().Be(AsistOff.MES.Production.Domain.Enums.DowntimeEventStatus.Open);
        result.DurationMinutes.Should().BeNull();
        result.EndedAt.Should().BeNull();
    }

    private static ProductionOrder OrderWithStatus(ProductionOrderStatus status) => new()
    {
        Id = Guid.NewGuid(),
        Code = "PO-001",
        ProductId = Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        RecipeVersionId = Guid.NewGuid(),
        PlannedQuantity = 100m,
        Status = status
    };
}
