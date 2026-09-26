using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Application.Features.ProductionConfirmations.Create;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Production.Infrastructure.Repositories;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Shared.Infrastructure.Interceptors;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using FluentAssertions;
using LinqKit;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

/// <summary>
/// Real-transaction atomicity proof for the operator confirmation fan-out
/// (issue #265, AC2): the confirmation row, all RW/PW movements, all
/// genealogy edges and the order status flip share one
/// <see cref="EfUnitOfWork"/> database transaction over the real
/// <see cref="DefaultContext"/>, so a mid-fan-out failure rolls back rows
/// that were already saved — the confirmation is then retrievable neither by
/// id nor by order browse.
///
/// This complements <c>CreateProductionConfirmationAtomicityTests</c> (which
/// proves the handler runs all four writes inside one <see cref="IUnitOfWork"/>
/// boundary with a snapshot/restore double) and <c>EfUnitOfWorkTests</c>
/// (which proves commit/rollback semantics on unrelated tables): here the
/// rollback is proven with a real BEGIN/ROLLBACK on the confirmation tables
/// themselves. SQLite in-memory is used because it is relational (real
/// transactions), unlike the InMemory provider.
///
/// A mid-fan-out failure cannot be forced over HTTP without a
/// fault-injection hook in production code, so the failure is simulated with
/// a repository decorator that persists its rows and then throws — exactly
/// the interleaving the issue describes (confirmation committed, movement
/// insert fails).
/// </summary>
public sealed class CreateProductionConfirmationTransactionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DefaultContext _context;
    private readonly EfUnitOfWork _unitOfWork;
    private readonly RecordingGuidProvider _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<IChildEntitiesRepository> _children = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    private readonly ProductionConfirmationsRepository _confirmations;
    private readonly ProductionOrdersRepository _orders;
    private readonly LotsRepository _lots;
    private readonly LotGenealogyEdgesRepository _edges;
    private readonly ContextStockMovementsRepository _movements;

    public CreateProductionConfirmationTransactionTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var tenantAccessor = new Mock<ICurrentTenantAccessor>();
        var capturedTenant = _tenantId;
        tenantAccessor.Setup(a => a.TryGetTenantId(out capturedTenant)).Returns(true);
        tenantAccessor.SetupGet(a => a.CurrentTenantId).Returns(_tenantId);

        var userAccessor = new Mock<ICurrentUserAccessor>();
        userAccessor.SetupGet(a => a.UserId).Returns((Guid?)null);

        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);

        var options = new DbContextOptionsBuilder<DefaultContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AtomicityTestContext(
            options,
            new PublishDomainEventsInterceptor(_clock.Object, _guids, tenantAccessor.Object),
            new AuditableEntityInterceptor(_clock.Object, userAccessor.Object),
            new AuditHistoryInterceptor(_clock.Object, userAccessor.Object, tenantAccessor.Object, _guids),
            new SaasyEntityInterceptor(tenantAccessor.Object),
            tenantAccessor.Object);

        _context.Database.EnsureCreated();

        _unitOfWork = new EfUnitOfWork(_context);
        _confirmations = new ProductionConfirmationsRepository(_context);
        _orders = new ProductionOrdersRepository(_context);
        _lots = new LotsRepository(_context);
        _edges = new LotGenealogyEdgesRepository(_context);
        _movements = new ContextStockMovementsRepository(_context);
    }

    [Fact]
    public async Task Handle_MovementFailureAfterPersist_RollsBackConfirmationMovementsAndOrderFlip()
    {
        // Arrange
        var order = await SeedReleasedOrderAsync();
        var bomItems = SingleBomItem();
        _children.Setup(r => r.ListBomItemsForVersionAsync(order.RecipeVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bomItems);
        var guidsBeforeHandle = _guids.Issued.Count;
        var sut = CreateSut(new FailAfterPersistStockMovements(_movements));

        // Act
        var act = () => sut.Handle(ValidRequest(order.Id), CancellationToken.None);

        // Assert — the mid-fan-out failure propagates even though both the
        // confirmation row and the movement lines were already saved inside
        // the transaction.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("simulated mid-fan-out movement failure");
        _context.ChangeTracker.Clear();

        var confirmationId = _guids.Issued[guidsBeforeHandle];
        (await _confirmations.GetAsync(confirmationId)).Should().BeNull("no orphan confirmation may survive by id");
        (await _confirmations.ListForOrderAsync(order.Id)).Should().BeEmpty("no orphan confirmation may survive by browse");
        (await _context.Set<StockMovement>().CountAsync(x => x.ProductionOrderId == order.Id))
            .Should().Be(0, "persisted RW/PW lines must roll back with the failed fan-out");
        (await _context.Set<LotGenealogyEdge>().CountAsync(x => x.ProductionOrderId == order.Id)).Should().Be(0);
        (await _orders.GetAsync(order.Id))!.Status.Should().Be(ProductionOrderStatus.Released);
    }

    [Fact]
    public async Task Handle_EdgeFailureAfterPersist_RollsBackConfirmationMovementsAndEdges()
    {
        // Arrange
        var order = await SeedReleasedOrderAsync();
        var produced = await SeedLotAsync("LOT-P-ATOM");
        var consumedA = await SeedLotAsync("LOT-C-ATOM-A");
        var consumedB = await SeedLotAsync("LOT-C-ATOM-B");
        _children.Setup(r => r.ListBomItemsForVersionAsync(order.RecipeVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BomItem>());
        var guidsBeforeHandle = _guids.Issued.Count;
        var sut = CreateSut(_movements, new FailAfterFirstEdgePersist(_edges));
        var request = ValidRequest(order.Id) with
        {
            ProducedLotId = produced.Id,
            ConsumedLots = new[]
            {
                new ConsumedLotEntry(consumedA.Id, 5m),
                new ConsumedLotEntry(consumedB.Id, 7m),
            },
        };

        // Act
        var act = () => sut.Handle(request, CancellationToken.None);

        // Assert — the first edge row was already saved when the second
        // persistence failed, so a correct rollback removes the confirmation,
        // the movements and the partial edge set together.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("simulated mid-fan-out edge failure");
        _context.ChangeTracker.Clear();

        var confirmationId = _guids.Issued[guidsBeforeHandle];
        (await _confirmations.GetAsync(confirmationId)).Should().BeNull("no orphan confirmation may survive by id");
        (await _confirmations.ListForOrderAsync(order.Id)).Should().BeEmpty("no orphan confirmation may survive by browse");
        (await _context.Set<StockMovement>().CountAsync(x => x.ProductionOrderId == order.Id))
            .Should().Be(0, "persisted RW/PW lines must roll back with the failed edge");
        (await _edges.ListByProducedLotIdsAsync([produced.Id])).Should().BeEmpty("the persisted first edge must roll back too");
        (await _orders.GetAsync(order.Id))!.Status.Should().Be(ProductionOrderStatus.Released);
    }

    [Fact]
    public async Task Handle_HappyPath_CommitsConfirmationMovementsEdgesAndOrderFlipInSingleTransaction()
    {
        // Arrange
        var order = await SeedReleasedOrderAsync(measureUnitId: Guid.NewGuid());
        var produced = await SeedLotAsync("LOT-P-HAPPY");
        var consumed = await SeedLotAsync("LOT-C-HAPPY");
        var bomItems = SingleBomItem();
        _children.Setup(r => r.ListBomItemsForVersionAsync(order.RecipeVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bomItems);
        var counting = new CountingUnitOfWork(_unitOfWork);
        var sut = CreateSut(_movements, null, counting);
        var request = ValidRequest(order.Id) with
        {
            ProducedLotId = produced.Id,
            ConsumedLots = new[] { new ConsumedLotEntry(consumed.Id, 5m) },
        };

        // Act
        var result = await sut.Handle(request, CancellationToken.None);

        // Assert — all four writes committed together inside one transaction,
        // readable in one reload, with persisted lines equal to the preview.
        counting.ExecuteCalls.Should().Be(1);
        _context.ChangeTracker.Clear();

        var saved = await _confirmations.GetAsync(result.Id);
        saved.Should().NotBeNull();
        saved!.ProductionOrderId.Should().Be(order.Id);
        saved.GoodQuantity.Should().Be(10m);
        saved.ScrapQuantity.Should().Be(2m);

        var expected = MovementCalculator.BuildPreview(order, 10m, 1, bomItems);
        var lines = await _movements.ListForConfirmationAsync(result.Id);
        lines.Should().HaveCount(expected.Count);
        foreach (var line in expected)
        {
            lines.Should().ContainSingle(m =>
                m.MovementType == line.MovementType
                && m.ProductId == line.ProductId
                && m.Quantity == line.Quantity
                && m.MeasureUnitId == line.MeasureUnitId
                && m.WarehouseId == line.PreferredWarehouseId);
        }

        var edges = await _edges.ListByProducedLotIdsAsync([produced.Id]);
        edges.Should().ContainSingle(e =>
            e.ConsumedLotId == consumed.Id
            && e.ConsumedQuantity == 5m
            && e.ProductionOrderId == order.Id
            && e.ProductionConfirmationId == result.Id);

        (await _orders.GetAsync(order.Id))!.Status.Should().Be(ProductionOrderStatus.InProgress);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private CreateProductionConfirmationRequestHandler CreateSut(
        IStockMovementsRepository movements,
        ILotGenealogyEdgesRepository? edges = null,
        IUnitOfWork? unitOfWork = null) =>
        new(_confirmations, _orders, _children.Object, movements,
            _lots, edges ?? _edges, _guids, _clock.Object, _tenant.Object,
            unitOfWork ?? _unitOfWork);

    private static CreateProductionConfirmationRequest ValidRequest(Guid orderId) => new(
        orderId,
        Guid.NewGuid(),
        null,
        new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc),
        10m,
        2m,
        null);

    private async Task<ProductionOrder> SeedReleasedOrderAsync(Guid? measureUnitId = null)
    {
        var order = new ProductionOrder
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Code = $"PO-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            ProductId = Guid.NewGuid(),
            RecipeId = Guid.NewGuid(),
            RecipeVersionId = Guid.NewGuid(),
            PlannedQuantity = 100m,
            MeasureUnitId = measureUnitId,
            Status = ProductionOrderStatus.Released,
            ReleasedAt = new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc),
            CreatedAt = _now,
        };
        _context.Set<ProductionOrder>().Add(order);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return order;
    }

    private async Task<Lot> SeedLotAsync(string code)
    {
        var lot = new Lot
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Code = code,
            ProductId = Guid.NewGuid(),
            MeasureUnitId = Guid.NewGuid(),
            Quantity = 100m,
            Status = LotStatus.Available,
            CreatedAt = _now,
        };
        return await _lots.AddAsync(lot);
    }

    private List<BomItem> SingleBomItem() =>
    [
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            OperationNodeId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            MeasureUnitId = Guid.NewGuid(),
            Quantity = 2m,
            QuantityType = BomQuantityType.PerUnit,
            PreferredWarehouseId = Guid.NewGuid(),
            SortIndex = 0,
        },
    ];

    /// <summary>
    /// Test-local <see cref="DefaultContext"/> subtype. EF Core caches the
    /// compiled model per context type, and <c>EfUnitOfWorkTests</c> builds a
    /// <see cref="DefaultContext"/> model with no entity configurators — a
    /// distinct runtime type gives this suite its own cached model that
    /// includes the confirmation fan-out tables.
    /// </summary>
    private sealed class AtomicityTestContext(
        DbContextOptions<DefaultContext> options,
        PublishDomainEventsInterceptor publishDomainEventsInterceptor,
        AuditableEntityInterceptor auditableEntityInterceptor,
        AuditHistoryInterceptor auditHistoryInterceptor,
        SaasyEntityInterceptor saasyEntityInterceptor,
        ICurrentTenantAccessor tenantAccessor)
        : DefaultContext(
            options,
            new IEntityConfigurator[] { new ConfirmationAtomicityConfigurator() },
            publishDomainEventsInterceptor,
            auditableEntityInterceptor,
            auditHistoryInterceptor,
            saasyEntityInterceptor,
            tenantAccessor);

    /// <summary>
    /// Minimal test-only mapping for the confirmation fan-out tables. Plain
    /// table names (no PostgreSQL schemas) and ignored navigation properties
    /// keep the model SQLite-safe; production mappings stay untouched.
    /// </summary>
    private sealed class ConfirmationAtomicityConfigurator : IEntityConfigurator
    {
        public void ConfigureEntities(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProductionOrder>(b =>
            {
                b.ToTable("TestProductionOrders");
                b.HasKey(x => x.Id);
            });
            modelBuilder.Entity<ProductionConfirmation>(b =>
            {
                b.ToTable("TestProductionConfirmations");
                b.HasKey(x => x.Id);
                b.Ignore(x => x.ProductionOrder);
            });
            modelBuilder.Entity<StockMovement>(b =>
            {
                b.ToTable("TestStockMovements");
                b.HasKey(x => x.Id);
            });
            modelBuilder.Entity<Lot>(b =>
            {
                b.ToTable("TestLots");
                b.HasKey(x => x.Id);
            });
            modelBuilder.Entity<LotGenealogyEdge>(b =>
            {
                b.ToTable("TestLotGenealogyEdges");
                b.HasKey(x => x.Id);
                b.Ignore(x => x.ConsumedLot);
                b.Ignore(x => x.ProducedLot);
                b.Ignore(x => x.ProductionOrder);
                b.Ignore(x => x.ProductionConfirmation);
            });
        }
    }

    /// <summary>
    /// Test-only <see cref="IStockMovementsRepository"/> over the same
    /// <see cref="DefaultContext"/>, mirroring the production semantics
    /// (empty short-circuit, confirmation browse ordering) so its writes
    /// enlist in the ambient <see cref="EfUnitOfWork"/> transaction.
    /// </summary>
    private sealed class ContextStockMovementsRepository(DefaultContext context) : IStockMovementsRepository
    {
        public async Task<IReadOnlyCollection<StockMovement>> ListForConfirmationAsync(
            Guid productionConfirmationId, CancellationToken cancellationToken = default) =>
            await context.Set<StockMovement>()
                .AsNoTracking()
                .Where(x => x.ProductionConfirmationId == productionConfirmationId)
                .OrderBy(x => x.MovementType)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyCollection<StockMovement>> ListAsync(
            CancellationToken cancellationToken = default) =>
            await context.Set<StockMovement>()
                .AsNoTracking()
                .OrderBy(x => x.ProductId)
                .ThenBy(x => x.WarehouseId)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

        public async Task AddRangeAsync(
            IReadOnlyCollection<StockMovement> entities, CancellationToken cancellationToken = default)
        {
            if (entities.Count == 0)
                return;

            context.Set<StockMovement>().AddRange(entities);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Simulates the issue's mid-fan-out movement failure: the ledger lines
    /// are really saved inside the transaction and only then does the
    /// persistence fail, so only a true rollback leaves zero rows behind.
    /// </summary>
    private sealed class FailAfterPersistStockMovements(IStockMovementsRepository inner) : IStockMovementsRepository
    {
        public Task<IReadOnlyCollection<StockMovement>> ListForConfirmationAsync(
            Guid productionConfirmationId, CancellationToken cancellationToken = default) =>
            inner.ListForConfirmationAsync(productionConfirmationId, cancellationToken);

        public Task<IReadOnlyCollection<StockMovement>> ListAsync(
            CancellationToken cancellationToken = default) =>
            inner.ListAsync(cancellationToken);

        public async Task AddRangeAsync(
            IReadOnlyCollection<StockMovement> entities, CancellationToken cancellationToken = default)
        {
            await inner.AddRangeAsync(entities, cancellationToken);
            throw new InvalidOperationException("simulated mid-fan-out movement failure");
        }
    }

    /// <summary>
    /// Simulates a mid-fan-out edge failure after the first edge row was
    /// already saved, proving partial edge sets roll back together with the
    /// confirmation and the movements.
    /// </summary>
    private sealed class FailAfterFirstEdgePersist(ILotGenealogyEdgesRepository inner) : ILotGenealogyEdgesRepository
    {
        private bool _failed;

        public Task<IReadOnlyCollection<LotGenealogyEdge>> BrowseAsync(
            Paginator<LotGenealogyEdge> paginator, CancellationToken cancellationToken = default) =>
            inner.BrowseAsync(paginator, cancellationToken);

        public Task<int> CountAsync(
            ExpressionStarter<LotGenealogyEdge> predicate, CancellationToken cancellationToken = default) =>
            inner.CountAsync(predicate, cancellationToken);

        public Task<LotGenealogyEdge?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            inner.GetAsync(id, cancellationToken);

        public async Task<LotGenealogyEdge> AddAsync(LotGenealogyEdge entity, CancellationToken cancellationToken = default)
        {
            var saved = await inner.AddAsync(entity, cancellationToken);
            if (!_failed)
            {
                _failed = true;
                throw new InvalidOperationException("simulated mid-fan-out edge failure");
            }

            return saved;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            inner.DeleteAsync(id, cancellationToken);

        public Task<IReadOnlyCollection<LotGenealogyEdge>> ListByProducedLotIdsAsync(
            IReadOnlyCollection<Guid> producedLotIds, CancellationToken cancellationToken = default) =>
            inner.ListByProducedLotIdsAsync(producedLotIds, cancellationToken);

        public Task<IReadOnlyCollection<LotGenealogyEdge>> ListByConsumedLotIdsAsync(
            IReadOnlyCollection<Guid> consumedLotIds, CancellationToken cancellationToken = default) =>
            inner.ListByConsumedLotIdsAsync(consumedLotIds, cancellationToken);

        public Task DeleteByConfirmationAsync(Guid productionConfirmationId, CancellationToken cancellationToken = default) =>
            inner.DeleteByConfirmationAsync(productionConfirmationId, cancellationToken);
    }

    /// <summary>
    /// <see cref="IGuidProvider"/> that records every issued id so tests can
    /// resolve the handler-generated confirmation id and assert it reads back
    /// as null after a rollback.
    /// </summary>
    private sealed class RecordingGuidProvider : IGuidProvider
    {
        public readonly List<Guid> Issued = new();

        public Guid NewGuid()
        {
            var id = Guid.NewGuid();
            Issued.Add(id);
            return id;
        }
    }

    /// <summary>
    /// Counts <see cref="IUnitOfWork"/> boundary crossings while delegating
    /// to the real <see cref="EfUnitOfWork"/>, proving the happy path commits
    /// all four writes inside exactly one real transaction.
    /// </summary>
    private sealed class CountingUnitOfWork(IUnitOfWork inner) : IUnitOfWork
    {
        public int ExecuteCalls;

        public Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
        {
            ExecuteCalls++;
            return inner.ExecuteAsync(action, cancellationToken);
        }

        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
        {
            ExecuteCalls++;
            return inner.ExecuteAsync(action, cancellationToken);
        }

        public Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default) =>
            inner.ExecuteInTransactionAsync(action, cancellationToken);
    }
}
