using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Shared.Infrastructure.Interceptors;
using AsistOff.MES.Shared.Infrastructure.Persistence.Entities;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Audit;

/// <summary>
/// Slice 2 (#246) history-writer coverage: create, update and delete of the
/// pilot entities append exactly one row each with entity name, entity id,
/// action, actor and timestamp; non-pilot writes append none; failed writes
/// leave no ghost history; tenant id flows from the ambient context; history
/// rows themselves are append-only.
/// </summary>
public class AuditHistoryInterceptorTests
{
    // NOTE: nested class names intentionally match the pilot entity names
    // (ProductionOrder/Machine/ProductionConfirmation) because the interceptor
    // detects pilots by CLR type name without referencing domain assemblies.
    private sealed class ProductionOrder
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Code { get; set; } = string.Empty;
    }

    private sealed class Machine
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Code { get; set; } = string.Empty;
    }

    private sealed class ProductionConfirmation
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public decimal GoodQuantity { get; set; }
    }

    private sealed class Widget
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class AuditTestContext(
        DbContextOptions<AuditTestContext> options,
        AuditHistoryInterceptor interceptor) : DbContext(options)
    {
        public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();
        public DbSet<Machine> Machines => Set<Machine>();
        public DbSet<ProductionConfirmation> Confirmations => Set<ProductionConfirmation>();
        public DbSet<Widget> Widgets => Set<Widget>();
        public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(interceptor);
        }
    }

    private sealed class FakeUserAccessor : ICurrentUserAccessor
    {
        public Guid? UserId { get; set; }
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

    private static AuditTestContext BuildContext(
        FakeUserAccessor users,
        FakeTenantAccessor tenants,
        FakeClock clock)
    {
        var options = new DbContextOptionsBuilder<AuditTestContext>()
            .UseInMemoryDatabase($"audit-history-{Guid.NewGuid()}")
            .Options;

        return new AuditTestContext(
            options,
            new AuditHistoryInterceptor(clock, users, tenants, new FakeGuids()));
    }

    [Fact]
    public async Task Create_PilotEntity_AppendsSingleCreatedRow()
    {
        // Arrange
        var callerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var clock = new FakeClock();
        using var ctx = BuildContext(
            new FakeUserAccessor { UserId = callerId },
            new FakeTenantAccessor { CurrentTenantId = tenantId },
            clock);
        var order = new ProductionOrder { Id = Guid.NewGuid(), TenantId = tenantId, Code = "PO-1" };

        // Act
        ctx.ProductionOrders.Add(order);
        await ctx.SaveChangesAsync();

        // Assert
        var rows = ctx.AuditEvents.ToList();
        rows.Should().ContainSingle();
        rows[0].EntityName.Should().Be("ProductionOrder");
        rows[0].EntityId.Should().Be(order.Id);
        rows[0].Action.Should().Be(AuditEventAction.Created);
        rows[0].ActorId.Should().Be(callerId);
        rows[0].ChangedAt.Should().Be(clock.UtcNow);
        rows[0].TenantId.Should().Be(tenantId);
        rows[0].Payload.Should().Contain("PO-1");
    }

    [Fact]
    public async Task Update_PilotEntity_AppendsSingleUpdatedRow_AndLeavesPriorRowUntouched()
    {
        // Arrange
        var callerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var clock = new FakeClock();
        using var ctx = BuildContext(
            new FakeUserAccessor { UserId = callerId },
            new FakeTenantAccessor { CurrentTenantId = tenantId },
            clock);
        var machine = new Machine { Id = Guid.NewGuid(), TenantId = tenantId, Code = "M-1" };
        ctx.Machines.Add(machine);
        await ctx.SaveChangesAsync();
        var createdChangedAt = ctx.AuditEvents.Single().ChangedAt;
        var createdPayload = ctx.AuditEvents.Single().Payload;
        clock.UtcNow = clock.UtcNow.AddHours(1);

        // Act
        machine.Code = "M-2";
        await ctx.SaveChangesAsync();

        // Assert — exactly one new row; the create row is never updated.
        var rows = ctx.AuditEvents.OrderBy(x => x.ChangedAt).ToList();
        rows.Should().HaveCount(2);
        rows.Should().ContainSingle(x => x.Action == AuditEventAction.Created);
        var updated = rows.Should().ContainSingle(x => x.Action == AuditEventAction.Updated).Subject;
        updated.EntityName.Should().Be("Machine");
        updated.EntityId.Should().Be(machine.Id);
        updated.ActorId.Should().Be(callerId);
        updated.ChangedAt.Should().Be(clock.UtcNow);
        updated.Payload.Should().Contain("M-2");

        rows.Should().ContainSingle(x =>
            x.Action == AuditEventAction.Created
            && x.ChangedAt == createdChangedAt
            && x.Payload == createdPayload);
    }

    [Fact]
    public async Task Delete_PilotEntity_AppendsSingleDeletedRow()
    {
        // Arrange
        var callerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        using var ctx = BuildContext(
            new FakeUserAccessor { UserId = callerId },
            new FakeTenantAccessor { CurrentTenantId = tenantId },
            new FakeClock());
        var confirmation = new ProductionConfirmation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            GoodQuantity = 5m,
        };
        ctx.Confirmations.Add(confirmation);
        await ctx.SaveChangesAsync();

        // Act — tracked remove, mirroring the repository delete path.
        ctx.Confirmations.Remove(confirmation);
        await ctx.SaveChangesAsync();

        // Assert
        var rows = ctx.AuditEvents.ToList();
        rows.Should().HaveCount(2);
        var deleted = rows.Should().ContainSingle(x => x.Action == AuditEventAction.Deleted).Subject;
        deleted.EntityName.Should().Be("ProductionConfirmation");
        deleted.EntityId.Should().Be(confirmation.Id);
        deleted.ActorId.Should().Be(callerId);
        deleted.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task Create_NonPilotEntity_AppendsNoRows()
    {
        // Arrange
        using var ctx = BuildContext(
            new FakeUserAccessor { UserId = Guid.NewGuid() },
            new FakeTenantAccessor { CurrentTenantId = Guid.NewGuid() },
            new FakeClock());

        // Act
        ctx.Widgets.Add(new Widget { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Name = "w" });
        await ctx.SaveChangesAsync();

        // Assert
        ctx.AuditEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscardedWrite_AppendsNoGhostHistory()
    {
        // Arrange — the primary write is rolled back before SaveChanges, so no
        // history may exist for it.
        using var ctx = BuildContext(
            new FakeUserAccessor { UserId = Guid.NewGuid() },
            new FakeTenantAccessor { CurrentTenantId = Guid.NewGuid() },
            new FakeClock());
        ctx.ProductionOrders.Add(new ProductionOrder
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Code = "PO-ROLLBACK",
        });

        // Act — simulate the rollback, then save the (now empty) unit of work.
        ctx.ChangeTracker.Clear();
        await ctx.SaveChangesAsync();

        // Assert
        ctx.AuditEvents.Should().BeEmpty();
        ctx.ProductionOrders.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_EntityWithoutTenant_FlowsAmbientTenantIntoHistoryRow()
    {
        // Arrange
        var ambient = Guid.NewGuid();
        using var ctx = BuildContext(
            new FakeUserAccessor { UserId = Guid.NewGuid() },
            new FakeTenantAccessor { CurrentTenantId = ambient },
            new FakeClock());
        var order = new ProductionOrder { Id = Guid.NewGuid(), TenantId = Guid.Empty, Code = "PO-1" };

        // Act
        ctx.ProductionOrders.Add(order);
        await ctx.SaveChangesAsync();

        // Assert
        ctx.AuditEvents.Should().ContainSingle(x => x.TenantId == ambient);
    }

    [Fact]
    public async Task Create_AnonymousWrite_PersistsNullActorWithoutFailing()
    {
        // Arrange — pre-authentication writes have no caller.
        using var ctx = BuildContext(
            new FakeUserAccessor { UserId = null },
            new FakeTenantAccessor { CurrentTenantId = Guid.NewGuid() },
            new FakeClock());

        // Act
        ctx.Machines.Add(new Machine { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Code = "M-1" });
        await ctx.SaveChangesAsync();

        // Assert
        ctx.AuditEvents.Should().ContainSingle(x => x.ActorId == null);
    }

    [Fact]
    public void SaveChanges_SyncPath_AppendsHistoryRow()
    {
        // Arrange — the sync override exists for completeness alongside the
        // async pipeline used in production.
        using var ctx = BuildContext(
            new FakeUserAccessor { UserId = Guid.NewGuid() },
            new FakeTenantAccessor { CurrentTenantId = Guid.NewGuid() },
            new FakeClock());
        var order = new ProductionOrder { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Code = "PO-1" };

        // Act
        ctx.ProductionOrders.Add(order);
        ctx.SaveChanges();

        // Assert
        ctx.AuditEvents.Should().ContainSingle(x =>
            x.EntityName == "ProductionOrder"
            && x.EntityId == order.Id
            && x.Action == AuditEventAction.Created);
    }

    [Theory]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public async Task Mutating_HistoryRow_Throws(EntityState mutation)
    {
        // Arrange — history rows are append-only: application code must never
        // update or delete them.
        using var ctx = BuildContext(
            new FakeUserAccessor { UserId = Guid.NewGuid() },
            new FakeTenantAccessor { CurrentTenantId = Guid.NewGuid() },
            new FakeClock());
        var order = new ProductionOrder { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Code = "PO-1" };
        ctx.ProductionOrders.Add(order);
        await ctx.SaveChangesAsync();
        var row = ctx.AuditEvents.Single();
        ctx.ChangeTracker.Clear();
        ctx.Attach(row);
        ctx.Entry(row).State = mutation;

        // Act
        var act = () => ctx.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*append-only*");
    }
}
