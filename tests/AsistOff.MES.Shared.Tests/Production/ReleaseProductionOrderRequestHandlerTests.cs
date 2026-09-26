using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.ProductionOrders.Release;
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

public class ReleaseProductionOrderRequestHandlerTests
{
    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private readonly Mock<IRecipeVersionsRepository> _versions = new();
    private readonly Mock<IChildEntitiesRepository> _children = new();
    private readonly Mock<IMaterialReservationsRepository> _reservations = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ICurrentUserAccessor> _user = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTime _now = DateTime.UtcNow;

    public ReleaseProductionOrderRequestHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _user.SetupGet(u => u.UserId).Returns(_userId);
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
        _children.Setup(r => r.ListBomItemsForVersionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BomItem>());
        _reservations.Setup(r => r.ListForOrderAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MaterialReservation>());
        _uow.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> op, CancellationToken _) => op());
    }

    private ReleaseProductionOrderRequestHandler CreateSut() =>
        new(_orders.Object, _versions.Object, _children.Object, _reservations.Object,
            _guids.Object, _clock.Object, _user.Object, _tenant.Object, _uow.Object);

    private static ProductionOrder MakeOrder(ProductionOrderStatus status) => new()
    {
        Id = Guid.NewGuid(),
        Code = "PO-001",
        ProductId = Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        RecipeVersionId = Guid.NewGuid(),
        PlannedQuantity = 10m,
        Status = status
    };

    [Fact]
    public async Task Throws_ConflictException_when_order_is_not_planned()
    {
        var order = MakeOrder(ProductionOrderStatus.Released);
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var act = () => CreateSut().Handle(new ReleaseProductionOrderRequest(order.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Throws_ValidationException_when_recipe_version_is_not_released()
    {
        var order = MakeOrder(ProductionOrderStatus.Planned);
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _versions.Setup(v => v.GetAsync(order.RecipeVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecipeVersion
            {
                Id = order.RecipeVersionId,
                RecipeId = order.RecipeId,
                VersionNumber = 1,
                Status = RecipeVersionStatus.Draft
            });

        var act = () => CreateSut().Handle(new ReleaseProductionOrderRequest(order.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Sets_released_status_released_at_and_released_by()
    {
        var order = MakeOrder(ProductionOrderStatus.Planned);
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _versions.Setup(v => v.GetAsync(order.RecipeVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecipeVersion
            {
                Id = order.RecipeVersionId,
                RecipeId = order.RecipeId,
                VersionNumber = 1,
                Status = RecipeVersionStatus.Released
            });

        var result = await CreateSut().Handle(new ReleaseProductionOrderRequest(order.Id), CancellationToken.None);

        order.Status.Should().Be(ProductionOrderStatus.Released);
        order.ReleasedAt.Should().Be(_now);
        order.ReleasedByUserId.Should().Be(_userId);
        result.Status.Should().Be(ProductionOrderStatus.Released);
        _orders.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
    }
}
