using System.Linq.Expressions;
using AsistOff.MES.Production.Application.Features.ProductionConfirmations.Browse;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Pagination;
using FluentAssertions;
using LinqKit;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class BrowseProductionConfirmationsRequestHandlerTests
{
    private readonly Mock<IProductionConfirmationsRepository> _confirmations = new();
    private ExpressionStarter<ProductionConfirmation> _captured = null!;

    public BrowseProductionConfirmationsRequestHandlerTests()
    {
        _confirmations.Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<ProductionConfirmation>>(), It.IsAny<CancellationToken>()))
            .Callback<ExpressionStarter<ProductionConfirmation>, CancellationToken>((p, _) => _captured = p)
            .ReturnsAsync(0);
        _confirmations.Setup(r => r.BrowseAsync(It.IsAny<Paginator<ProductionConfirmation>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductionConfirmation>());
    }

    private BrowseProductionConfirmationsRequestHandler CreateSut() => new(_confirmations.Object);

    private bool Matches(ProductionConfirmation confirmation)
    {
        var compiled = ((Expression<Func<ProductionConfirmation, bool>>)_captured).Compile();
        return compiled(confirmation);
    }

    private static ProductionConfirmation MakeConfirmation(
        Guid? productionOrderId = null,
        Guid? machineId = null,
        DateTime? reportedAt = null) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ProductionOrderId = productionOrderId ?? Guid.NewGuid(),
            MachineId = machineId ?? Guid.NewGuid(),
            ReportedAt = reportedAt ?? new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc),
            GoodQuantity = 5m,
            ScrapQuantity = 1m,
            CreatedAt = new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc)
        };

    [Fact]
    public async Task Applies_productionOrderId_filter()
    {
        var orderId = Guid.NewGuid();

        await CreateSut().Handle(new BrowseProductionConfirmationsRequest { ProductionOrderId = orderId }, CancellationToken.None);

        Matches(MakeConfirmation(productionOrderId: orderId)).Should().BeTrue();
        Matches(MakeConfirmation(productionOrderId: Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task Applies_machineId_filter()
    {
        var machineId = Guid.NewGuid();

        await CreateSut().Handle(new BrowseProductionConfirmationsRequest { MachineId = machineId }, CancellationToken.None);

        Matches(MakeConfirmation(machineId: machineId)).Should().BeTrue();
        Matches(MakeConfirmation(machineId: Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task Applies_reportedAt_range_filter()
    {
        var from = new DateTime(2026, 9, 24, 9, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 9, 24, 11, 0, 0, DateTimeKind.Utc);

        await CreateSut().Handle(new BrowseProductionConfirmationsRequest { From = from, To = to }, CancellationToken.None);

        Matches(MakeConfirmation(reportedAt: new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc))).Should().BeTrue();
        Matches(MakeConfirmation(reportedAt: new DateTime(2026, 9, 24, 8, 59, 0, DateTimeKind.Utc))).Should().BeFalse();
        Matches(MakeConfirmation(reportedAt: new DateTime(2026, 9, 24, 11, 1, 0, DateTimeKind.Utc))).Should().BeFalse();
    }

    [Fact]
    public async Task No_filters_matches_all()
    {
        await CreateSut().Handle(new BrowseProductionConfirmationsRequest(), CancellationToken.None);

        Matches(MakeConfirmation()).Should().BeTrue();
        Matches(MakeConfirmation()).Should().BeTrue();
    }

    [Fact]
    public async Task Returns_mapped_items_with_total_count()
    {
        var confirmation = MakeConfirmation();
        _confirmations.Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<ProductionConfirmation>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _confirmations.Setup(r => r.BrowseAsync(It.IsAny<Paginator<ProductionConfirmation>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductionConfirmation> { confirmation });

        var result = await CreateSut().Handle(new BrowseProductionConfirmationsRequest(), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(item =>
            item.Id == confirmation.Id &&
            item.ProductionOrderId == confirmation.ProductionOrderId &&
            item.MachineId == confirmation.MachineId &&
            item.GoodQuantity == confirmation.GoodQuantity &&
            item.ScrapQuantity == confirmation.ScrapQuantity);
    }
}
