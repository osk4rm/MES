using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.ScrapEvents.Create;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class CreateScrapEventRequestHandlerTests
{
    private readonly Mock<IScrapEventsRepository> _repository = new();
    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    public CreateScrapEventRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
    }

    private CreateScrapEventRequestHandler CreateSut() =>
        new(_repository.Object, _orders.Object, _guids.Object, _clock.Object, _tenant.Object);

    private static CreateScrapEventRequest ValidRequest() => new(
        Guid.NewGuid(), Guid.NewGuid(), 5m,
        new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc),
        "broken parts", null, null);

    [Fact]
    public async Task Handle_ZeroQuantity_ThrowsValidationException()
    {
        var request = ValidRequest() with { Quantity = 0 };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_NegativeQuantity_ThrowsValidationException()
    {
        var request = ValidRequest() with { Quantity = -2.5m };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_FutureReportedAt_ThrowsValidationException()
    {
        var request = ValidRequest() with { ReportedAt = _now.AddHours(1) };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_ReleasedOrderLink_StoresLink()
    {
        var order = OrderWithStatus(ProductionOrderStatus.Released);
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var request = ValidRequest() with { ProductionOrderId = order.Id };
        ScrapEvent? saved = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<ScrapEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ScrapEvent, CancellationToken>((e, _) => saved = e)
            .ReturnsAsync((ScrapEvent e, CancellationToken _) => e);

        var result = await CreateSut().Handle(request, CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.ProductionOrderId.Should().Be(order.Id);
        result.ProductionOrderId.Should().Be(order.Id);
    }

    [Fact]
    public async Task Handle_InProgressOrderLink_StoresLink()
    {
        var order = OrderWithStatus(ProductionOrderStatus.InProgress);
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var request = ValidRequest() with { ProductionOrderId = order.Id };
        _repository.Setup(r => r.AddAsync(It.IsAny<ScrapEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScrapEvent e, CancellationToken _) => e);

        var result = await CreateSut().Handle(request, CancellationToken.None);

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
        var request = ValidRequest() with { ProductionOrderId = order.Id };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_UnknownOrderLink_ThrowsNotFoundException()
    {
        // The tenant global query filter hides cross-tenant orders, so both
        // unknown and cross-tenant ids surface as null here.
        _orders.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductionOrder?)null);
        var request = ValidRequest() with { ProductionOrderId = Guid.NewGuid() };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
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

    [Fact]
    public async Task Handle_EmptyReasonCodeId_ThrowsValidationException()
    {
        var request = ValidRequest() with { ReasonCodeId = Guid.Empty };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_HappyPath_PersistsQuantityMachineReasonCodeAndTenant()
    {
        var request = ValidRequest();
        ScrapEvent? saved = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<ScrapEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ScrapEvent, CancellationToken>((e, _) => saved = e)
            .ReturnsAsync((ScrapEvent e, CancellationToken _) => e);

        var result = await CreateSut().Handle(request, CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.Quantity.Should().Be(request.Quantity);
        saved.MachineId.Should().Be(request.MachineId);
        saved.ReasonCodeId.Should().Be(request.ReasonCodeId);
        saved.TenantId.Should().Be(_tenantId);
        saved.ProductionOrderId.Should().BeNull();
        result.Quantity.Should().Be(request.Quantity);
        result.MachineId.Should().Be(request.MachineId);
        result.ReasonCodeId.Should().Be(request.ReasonCodeId);
    }
}
