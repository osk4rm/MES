using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Models.DomainEvents;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Shared.Infrastructure.Interceptors;
using AsistOff.MES.Shared.Infrastructure.Outbox;
using AsistOff.MES.Shared.Infrastructure.Persistence.Entities;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Outbox;

/// <summary>
/// Slice 2 (#259) interceptor coverage: saving an entity with domain events
/// stages one undispatched outbox row per event in the same save and never
/// publishes — delivery is the relay's job after commit, so a rolled-back
/// write leaves no staged rows and produces zero dispatches. Empty event
/// lists stage nothing; the tenant flows from the entity (falling back to the
/// ambient tenant); secret events reject the save.
/// </summary>
public class OutboxInterceptorTests
{
    private sealed record OrderPlacedEvent(Guid OrderId, string Code) : IDomainEvent;

    private sealed record OrderNoteAddedEvent(Guid OrderId, string Note) : IDomainEvent;

    private sealed record PasswordChangedEvent(Guid UserId, string Password) : IDomainEvent;

    private sealed class Order : IEntity, ISaasy, IHasDomainEvents
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Code { get; set; } = string.Empty;

        private readonly List<IDomainEvent> _domainEvents = new();
        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        public void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
        public void ClearDomainEvents() => _domainEvents.Clear();
    }

    private sealed class OutboxTestContext(
        DbContextOptions<OutboxTestContext> options,
        PublishDomainEventsInterceptor interceptor) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(interceptor);
        }
    }

    private sealed class OutboxlessContext(
        DbContextOptions<OutboxlessContext> options,
        PublishDomainEventsInterceptor interceptor) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(interceptor);
        }
    }

    private sealed class FakeTenantAccessor : ICurrentTenantAccessor
    {
        public Guid CurrentTenantId { get; set; }

        public bool TryGetTenantId(out Guid tenantId)
        {
            tenantId = CurrentTenantId;
            return CurrentTenantId != Guid.Empty;
        }
    }

    private sealed class FakeClock : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = new(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc);
    }

    private sealed class FakeGuids : IGuidProvider
    {
        public Guid NewGuid() => Guid.NewGuid();
    }

    private readonly FakeTenantAccessor _tenants = new();
    private readonly FakeClock _clock = new();

    private PublishDomainEventsInterceptor BuildInterceptor() =>
        new(_clock, new FakeGuids(), _tenants);

    private OutboxTestContext BuildContext() =>
        new(new DbContextOptionsBuilder<OutboxTestContext>()
            .UseInMemoryDatabase($"outbox-{Guid.NewGuid()}")
            .Options,
            BuildInterceptor());

    [Fact]
    public async Task Save_WithSingleDomainEvent_StagesSingleUndispatchedRow_AndNeverPublishes()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenants.CurrentTenantId = tenantId;
        using var ctx = BuildContext();
        var order = new Order { Id = Guid.NewGuid(), TenantId = tenantId, Code = "PO-1" };
        order.AddDomainEvent(new OrderPlacedEvent(order.Id, order.Code));

        // Act
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        // Assert — one staged row with type, payload, tenant and undispatched status.
        var rows = ctx.OutboxMessages.ToList();
        rows.Should().ContainSingle();
        rows[0].TenantId.Should().Be(tenantId);
        rows[0].Type.Should().Contain(nameof(OrderPlacedEvent));
        rows[0].Payload.Should().Contain("PO-1");
        rows[0].OccurredOnUtc.Should().Be(_clock.UtcNow);
        rows[0].Dispatched.Should().BeFalse();
        rows[0].RetryCount.Should().Be(0);
        rows[0].IdempotencyKey.Should().NotBeNullOrWhiteSpace();

        // Assert — nothing is published pre-commit; the relay delivers after commit.
        order.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_WithMultipleEvents_StagesOneRowPerEvent()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenants.CurrentTenantId = tenantId;
        using var ctx = BuildContext();
        var order = new Order { Id = Guid.NewGuid(), TenantId = tenantId, Code = "PO-2" };
        order.AddDomainEvent(new OrderPlacedEvent(order.Id, order.Code));
        order.AddDomainEvent(new OrderNoteAddedEvent(order.Id, "rush"));

        // Act
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        // Assert
        var rows = ctx.OutboxMessages.ToList();
        rows.Should().HaveCount(2);
        rows.Select(x => x.IdempotencyKey).Should().OnlyHaveUniqueItems();
        rows.Select(x => x.Type).Should().Contain(t => t.Contains(nameof(OrderPlacedEvent)));
        rows.Select(x => x.Type).Should().Contain(t => t.Contains(nameof(OrderNoteAddedEvent)));
        order.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_WithoutEvents_StagesNothing()
    {
        // Arrange
        _tenants.CurrentTenantId = Guid.NewGuid();
        using var ctx = BuildContext();

        // Act
        ctx.Orders.Add(new Order { Id = Guid.NewGuid(), TenantId = _tenants.CurrentTenantId, Code = "PO-3" });
        await ctx.SaveChangesAsync();

        // Assert
        ctx.OutboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscardedWrite_StagesNoGhostRows_AndDispatchesNothing()
    {
        // Arrange — the primary write is rolled back before SaveChanges, so no
        // staged rows may exist for it and the relay can never dispatch it.
        _tenants.CurrentTenantId = Guid.NewGuid();
        using var ctx = BuildContext();
        var order = new Order { Id = Guid.NewGuid(), TenantId = _tenants.CurrentTenantId, Code = "PO-ROLLBACK" };
        order.AddDomainEvent(new OrderPlacedEvent(order.Id, order.Code));
        ctx.Orders.Add(order);

        // Act — simulate the rollback, then save the (now empty) unit of work.
        ctx.ChangeTracker.Clear();
        await ctx.SaveChangesAsync();

        // Assert — no staged rows, hence zero possible dispatches.
        ctx.OutboxMessages.Should().BeEmpty();
        ctx.Orders.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_EntityWithoutTenant_FlowsAmbientTenantIntoStagedRow()
    {
        // Arrange
        var ambient = Guid.NewGuid();
        _tenants.CurrentTenantId = ambient;
        using var ctx = BuildContext();
        var order = new Order { Id = Guid.NewGuid(), TenantId = Guid.Empty, Code = "PO-4" };
        order.AddDomainEvent(new OrderPlacedEvent(order.Id, order.Code));

        // Act
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        // Assert
        ctx.OutboxMessages.Should().ContainSingle(x => x.TenantId == ambient);
    }

    [Fact]
    public async Task Save_WithoutAnyTenant_Throws()
    {
        // Arrange — neither the entity nor the ambient context has a tenant.
        _tenants.CurrentTenantId = Guid.Empty;
        using var ctx = BuildContext();
        var order = new Order { Id = Guid.NewGuid(), TenantId = Guid.Empty, Code = "PO-5" };
        order.AddDomainEvent(new OrderPlacedEvent(order.Id, order.Code));
        ctx.Orders.Add(order);

        // Act
        var act = () => ctx.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tenant*");
        ctx.OutboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_SecretEvent_RejectsWriteWithClearError()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenants.CurrentTenantId = tenantId;
        using var ctx = BuildContext();
        var order = new Order { Id = Guid.NewGuid(), TenantId = tenantId, Code = "PO-6" };
        order.AddDomainEvent(new PasswordChangedEvent(Guid.NewGuid(), "s3cret"));
        ctx.Orders.Add(order);

        // Act
        var act = () => ctx.SaveChangesAsync();

        // Assert — the write is rejected before anything is staged.
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Password*");
        ctx.OutboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_ContextWithoutOutboxSet_SkipsStagingAndPublishesNothing()
    {
        // Arrange — mirrors MultitenancyDbContext, whose model has no outbox set.
        _tenants.CurrentTenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<OutboxlessContext>()
            .UseInMemoryDatabase($"outboxless-{Guid.NewGuid()}")
            .Options;
        using var ctx = new OutboxlessContext(options, BuildInterceptor());
        var order = new Order { Id = Guid.NewGuid(), TenantId = _tenants.CurrentTenantId, Code = "PO-7" };
        order.AddDomainEvent(new OrderPlacedEvent(order.Id, order.Code));

        // Act
        ctx.Orders.Add(order);
        var act = () => ctx.SaveChangesAsync();

        // Assert — no staging crash, entity saved, events dropped without delivery.
        await act.Should().NotThrowAsync();
        ctx.Orders.Should().ContainSingle();
        order.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task StagedRows_AreReadableOldestFirst_WithBoundedBatch()
    {
        // Arrange — three saves at increasing occurrence times.
        var tenantId = Guid.NewGuid();
        _tenants.CurrentTenantId = tenantId;
        using var ctx = BuildContext();
        for (var i = 0; i < 3; i++)
        {
            var order = new Order { Id = Guid.NewGuid(), TenantId = tenantId, Code = $"PO-B{i}" };
            order.AddDomainEvent(new OrderPlacedEvent(order.Id, order.Code));
            ctx.Orders.Add(order);
            await ctx.SaveChangesAsync();
            _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        }

        // Act
        var page = OutboxStager.ApplyUndispatched(ctx.OutboxMessages, 2).ToList();

        // Assert
        page.Should().HaveCount(2);
        page.Select(x => x.OccurredOnUtc).Should().BeInAscendingOrder();
    }
}
