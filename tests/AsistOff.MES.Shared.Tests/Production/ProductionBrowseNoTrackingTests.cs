using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Infrastructure.Configurations;
using AsistOff.MES.Production.Infrastructure.Repositories;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Shared.Infrastructure.Interceptors;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using FluentAssertions;
using LinqKit;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

/// <summary>
/// EF-level proof for issue #274 AC3: the production browse read paths for
/// orders, confirmations, lots, and genealogy edges run with
/// <c>AsNoTracking</c>. Mock-based handler tests cannot observe tracking, so
/// these tests drive the real repository implementations against an EF Core
/// InMemory <see cref="DefaultContext"/> (no Docker needed), seed rows, run
/// each browse, and assert the change tracker stayed empty. Removing
/// <c>AsNoTracking</c> from any covered path turns the corresponding test
/// red because the returned rows become tracked.
/// </summary>
public sealed class ProductionBrowseNoTrackingTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private sealed class UnpagedRequest : IPagedRequest
    {
        public int? PageNumber => null;
        public int? PageSize => null;
        public int? MaxPageSize => 100;
        public List<string> RawSort { get; set; } = new();
        public IReadOnlyCollection<string> SupportedSortFields => Array.Empty<string>();
    }

    /// <summary>
    /// Test-only <see cref="DefaultContext"/> subtype. EF Core caches the
    /// model per context type process-wide, and other suites build
    /// <see cref="DefaultContext"/> with different configurators — a distinct
    /// subtype guarantees these tests always resolve a model containing the
    /// production entities regardless of test execution order.
    /// </summary>
    private sealed class NoTrackingContext(
        DbContextOptions<DefaultContext> options,
        IEnumerable<IEntityConfigurator> configurators,
        PublishDomainEventsInterceptor publish,
        AuditableEntityInterceptor auditable,
        AuditHistoryInterceptor auditHistory,
        SaasyEntityInterceptor saasy,
        ICurrentTenantAccessor tenants)
        : DefaultContext(options, configurators, publish, auditable, auditHistory, saasy, tenants);

    private static DefaultContext BuildContext(string dbName)
    {
        var tenantAccessor = new Mock<ICurrentTenantAccessor>();
        tenantAccessor.SetupGet(a => a.CurrentTenantId).Returns(TenantId);
        Guid ambient = TenantId;
        tenantAccessor.Setup(a => a.TryGetTenantId(out ambient)).Returns(true);

        var clock = Mock.Of<IDateTimeProvider>(d =>
            d.UtcNow == new DateTime(2026, 9, 22, 8, 0, 0, DateTimeKind.Utc));
        var userAccessor = Mock.Of<ICurrentUserAccessor>(a => a.UserId == null);
        var guidProvider = new Mock<IGuidProvider>();
        guidProvider.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());

        var options = new DbContextOptionsBuilder<DefaultContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        IEntityConfigurator[] configurators =
        [
            new ProductionEntityConfigurator(),
            new SharedAuditEntityConfigurator(),
        ];

        return new NoTrackingContext(
            options,
            configurators,
            new PublishDomainEventsInterceptor(clock, guidProvider.Object, tenantAccessor.Object),
            new AuditableEntityInterceptor(clock, userAccessor),
            new AuditHistoryInterceptor(clock, userAccessor, tenantAccessor.Object, guidProvider.Object),
            new SaasyEntityInterceptor(tenantAccessor.Object),
            tenantAccessor.Object);
    }

    private static ProductionOrder MakeOrder(
        string code,
        ProductionOrderStatus status = ProductionOrderStatus.Released,
        DateTime? dueDate = null) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Code = code,
            ProductId = Guid.NewGuid(),
            RecipeId = Guid.NewGuid(),
            RecipeVersionId = Guid.NewGuid(),
            PlannedQuantity = 10m,
            Status = status,
            DueDate = dueDate
        };

    private static Lot MakeLot(string code) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        Code = code,
        ProductId = Guid.NewGuid(),
        MeasureUnitId = Guid.NewGuid(),
        Quantity = 25m,
        Status = LotStatus.Available
    };

    private static ProductionConfirmation MakeConfirmation(Guid orderId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        ProductionOrderId = orderId,
        MachineId = Guid.NewGuid(),
        ReportedAt = new DateTime(2026, 9, 22, 8, 0, 0, DateTimeKind.Utc),
        GoodQuantity = 5m,
        ScrapQuantity = 0m
    };

    private static LotGenealogyEdge MakeEdge(Guid consumedLotId, Guid producedLotId, Guid orderId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        ConsumedLotId = consumedLotId,
        ProducedLotId = producedLotId,
        ProductionOrderId = orderId,
        MachineId = Guid.NewGuid(),
        ConsumedQuantity = 2m,
        OccurredAt = new DateTime(2026, 9, 22, 9, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task OrdersBrowse_LeavesNothingTracked()
    {
        // Arrange
        using var ctx = BuildContext($"notracking-orders-browse-{Guid.NewGuid()}");
        ctx.Set<ProductionOrder>().AddRange(MakeOrder("PO-NT-001"), MakeOrder("PO-NT-002"));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        var paginator = new Paginator<ProductionOrder>(
            PredicateBuilder.New<ProductionOrder>(true), new UnpagedRequest());

        // Act
        var rows = await new ProductionOrdersRepository(ctx).BrowseAsync(paginator);

        // Assert - rows come back but none are tracked.
        rows.Should().HaveCount(2);
        ctx.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task OrdersDispatchBrowse_LeavesNothingTracked()
    {
        // Arrange
        using var ctx = BuildContext($"notracking-orders-dispatch-{Guid.NewGuid()}");
        ctx.Set<ProductionOrder>().AddRange(
            MakeOrder("PO-NT-OVERDUE", ProductionOrderStatus.Released,
                new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc)),
            MakeOrder("PO-NT-WINDOW", ProductionOrderStatus.Released,
                new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc)));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        // Act
        var rows = (await new ProductionOrdersRepository(ctx).BrowseDispatchBoardAsync(
            new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 27), 200)).ToList();

        // Assert - bounded overdue-first page with no tracked entities.
        rows.Should().HaveCount(2);
        rows[0].Code.Should().Be("PO-NT-OVERDUE");
        ctx.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task ConfirmationsBrowse_LeavesNothingTracked()
    {
        // Arrange
        using var ctx = BuildContext($"notracking-confirmations-{Guid.NewGuid()}");
        var order = MakeOrder("PO-NT-CFM");
        ctx.Set<ProductionOrder>().Add(order);
        ctx.Set<ProductionConfirmation>().AddRange(
            MakeConfirmation(order.Id), MakeConfirmation(order.Id));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        var paginator = new Paginator<ProductionConfirmation>(
            PredicateBuilder.New<ProductionConfirmation>(true), new UnpagedRequest());

        // Act
        var rows = await new ProductionConfirmationsRepository(ctx).BrowseAsync(paginator);

        // Assert
        rows.Should().HaveCount(2);
        ctx.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task LotsBrowse_LeavesNothingTracked()
    {
        // Arrange
        using var ctx = BuildContext($"notracking-lots-{Guid.NewGuid()}");
        ctx.Set<Lot>().AddRange(MakeLot("LOT-NT-001"), MakeLot("LOT-NT-002"));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        var paginator = new Paginator<Lot>(
            PredicateBuilder.New<Lot>(true), new UnpagedRequest());

        // Act
        var rows = await new LotsRepository(ctx).BrowseAsync(paginator);

        // Assert
        rows.Should().HaveCount(2);
        ctx.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task GenealogyBrowse_LeavesNothingTracked()
    {
        // Arrange
        using var ctx = BuildContext($"notracking-genealogy-browse-{Guid.NewGuid()}");
        var ids = await SeedGenealogyEdgeAsync(ctx);
        ctx.ChangeTracker.Clear();
        var paginator = new Paginator<LotGenealogyEdge>(
            PredicateBuilder.New<LotGenealogyEdge>(true), new UnpagedRequest());

        // Act
        var rows = await new LotGenealogyEdgesRepository(ctx).BrowseAsync(paginator);

        // Assert
        rows.Should().ContainSingle().Which.Id.Should().Be(ids.EdgeId);
        ctx.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task GenealogyListByProducedLotIds_LeavesNothingTracked()
    {
        // Arrange
        using var ctx = BuildContext($"notracking-genealogy-produced-{Guid.NewGuid()}");
        var ids = await SeedGenealogyEdgeAsync(ctx);
        ctx.ChangeTracker.Clear();

        // Act
        var rows = await new LotGenealogyEdgesRepository(ctx)
            .ListByProducedLotIdsAsync([ids.ProducedLotId]);

        // Assert
        rows.Should().ContainSingle().Which.Id.Should().Be(ids.EdgeId);
        ctx.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task GenealogyListByConsumedLotIds_LeavesNothingTracked()
    {
        // Arrange
        using var ctx = BuildContext($"notracking-genealogy-consumed-{Guid.NewGuid()}");
        var ids = await SeedGenealogyEdgeAsync(ctx);
        ctx.ChangeTracker.Clear();

        // Act
        var rows = await new LotGenealogyEdgesRepository(ctx)
            .ListByConsumedLotIdsAsync([ids.ConsumedLotId]);

        // Assert
        rows.Should().ContainSingle().Which.Id.Should().Be(ids.EdgeId);
        ctx.ChangeTracker.Entries().Should().BeEmpty();
    }

    private static async Task<(Guid EdgeId, Guid ConsumedLotId, Guid ProducedLotId)> SeedGenealogyEdgeAsync(
        DefaultContext ctx)
    {
        var order = MakeOrder("PO-NT-GEN");
        var consumed = MakeLot("LOT-NT-COMP");
        var produced = MakeLot("LOT-NT-FG");
        var edge = MakeEdge(consumed.Id, produced.Id, order.Id);
        ctx.Set<ProductionOrder>().Add(order);
        ctx.Set<Lot>().AddRange(consumed, produced);
        ctx.Set<LotGenealogyEdge>().Add(edge);
        await ctx.SaveChangesAsync();
        return (edge.Id, consumed.Id, produced.Id);
    }
}
