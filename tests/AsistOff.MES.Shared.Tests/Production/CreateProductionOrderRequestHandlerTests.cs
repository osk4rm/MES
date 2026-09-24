using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.ProductionOrders.Create;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class CreateProductionOrderRequestHandlerTests
{
    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    public CreateProductionOrderRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
    }

    private CreateProductionOrderRequestHandler CreateSut() =>
        new(_orders.Object, _guids.Object, _tenant.Object);

    private static CreateProductionOrderRequest ValidRequest(string code = "PO-001") =>
        new(code, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, null, 0, null, null, null);

    [Fact]
    public async Task Throws_ValidationException_when_code_is_empty()
    {
        var act = () => CreateSut().Handle(ValidRequest(""), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Throws_ConflictException_when_code_already_exists()
    {
        _orders.Setup(r => r.CodeExistsAsync("PO-001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => CreateSut().Handle(ValidRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Creates_order_in_Planned_status_with_tenant_from_context()
    {
        _orders.Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        ProductionOrder? saved = null;
        _orders.Setup(r => r.AddAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()))
            .Callback<ProductionOrder, CancellationToken>((e, _) => saved = e)
            .ReturnsAsync((ProductionOrder e, CancellationToken _) => e);

        var result = await CreateSut().Handle(ValidRequest(), CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.Status.Should().Be(ProductionOrderStatus.Planned);
        saved.TenantId.Should().Be(_tenantId);
        result.Status.Should().Be(ProductionOrderStatus.Planned);
    }
}
