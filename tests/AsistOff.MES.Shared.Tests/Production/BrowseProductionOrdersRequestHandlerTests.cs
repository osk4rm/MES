using System.Linq.Expressions;
using AsistOff.MES.Production.Application.Features.ProductionOrders.Browse;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Pagination;
using FluentAssertions;
using LinqKit;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class BrowseProductionOrdersRequestHandlerTests
{
    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private ExpressionStarter<ProductionOrder> _captured = null!;

    public BrowseProductionOrdersRequestHandlerTests()
    {
        _orders.Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<ProductionOrder>>(), It.IsAny<CancellationToken>()))
            .Callback<ExpressionStarter<ProductionOrder>, CancellationToken>((p, _) => _captured = p)
            .ReturnsAsync(0);
        _orders.Setup(r => r.BrowseAsync(It.IsAny<Paginator<ProductionOrder>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductionOrder>());
    }

    private BrowseProductionOrdersRequestHandler CreateSut() => new(_orders.Object);

    private bool Matches(ProductionOrder order)
    {
        var compiled = ((Expression<Func<ProductionOrder, bool>>)_captured).Compile();
        return compiled(order);
    }

    private static ProductionOrder MakeOrder(
        string code = "PO-001",
        ProductionOrderStatus status = ProductionOrderStatus.Planned,
        Guid? productId = null,
        Guid? recipeId = null,
        DateTime? dueDate = null) => new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            ProductId = productId ?? Guid.NewGuid(),
            RecipeId = recipeId ?? Guid.NewGuid(),
            RecipeVersionId = Guid.NewGuid(),
            PlannedQuantity = 10m,
            Status = status,
            DueDate = dueDate
        };

    [Fact]
    public async Task Applies_code_filter()
    {
        await CreateSut().Handle(new BrowseProductionOrdersRequest { Code = "ABC" }, CancellationToken.None);

        Matches(MakeOrder(code: "XX-ABC-YY")).Should().BeTrue();
        Matches(MakeOrder(code: "XYZ")).Should().BeFalse();
    }

    [Fact]
    public async Task Applies_status_filter()
    {
        await CreateSut().Handle(
            new BrowseProductionOrdersRequest { Status = ProductionOrderStatus.Released }, CancellationToken.None);

        Matches(MakeOrder(status: ProductionOrderStatus.Released)).Should().BeTrue();
        Matches(MakeOrder(status: ProductionOrderStatus.Planned)).Should().BeFalse();
    }

    [Fact]
    public async Task Applies_product_and_recipe_filters()
    {
        var productId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();

        await CreateSut().Handle(
            new BrowseProductionOrdersRequest { ProductId = productId, RecipeId = recipeId },
            CancellationToken.None);

        Matches(MakeOrder(productId: productId, recipeId: recipeId)).Should().BeTrue();
        Matches(MakeOrder(productId: Guid.NewGuid(), recipeId: recipeId)).Should().BeFalse();
        Matches(MakeOrder(productId: productId, recipeId: Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task Applies_due_date_range_filter()
    {
        var from = new DateTime(2026, 01, 10, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 01, 20, 0, 0, 0, DateTimeKind.Utc);

        await CreateSut().Handle(
            new BrowseProductionOrdersRequest { DueFrom = from, DueTo = to }, CancellationToken.None);

        Matches(MakeOrder(dueDate: new DateTime(2026, 01, 15, 0, 0, 0, DateTimeKind.Utc))).Should().BeTrue();
        Matches(MakeOrder(dueDate: new DateTime(2026, 02, 01, 0, 0, 0, DateTimeKind.Utc))).Should().BeFalse();
    }
}
