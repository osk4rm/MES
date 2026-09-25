using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Shared.Infrastructure.Interceptors;
using AsistOff.MES.Shared.Infrastructure.Persistence.Entities;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Multitenancy;

public class AuditableEntityInterceptorTests
{
    private sealed class AuditedWidget : IEntity, ISaasy, IAuditable
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? ModifiedBy { get; set; }
    }

    private sealed class TestContext : DbContext
    {
        private readonly AuditableEntityInterceptor _interceptor;

        public DbSet<AuditedWidget> Widgets => Set<AuditedWidget>();

        public TestContext(DbContextOptions<TestContext> options, AuditableEntityInterceptor interceptor)
            : base(options)
        {
            _interceptor = interceptor;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(_interceptor);
        }
    }

    private sealed class FakeUserAccessor : ICurrentUserAccessor
    {
        public Guid? UserId { get; set; }
    }

    private static TestContext BuildContext(
        FakeUserAccessor accessor,
        DateTime utcNow,
        out Mock<IDateTimeProvider> clock)
    {
        clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.UtcNow).Returns(utcNow);

        var options = new DbContextOptionsBuilder<TestContext>()
            .UseInMemoryDatabase($"auditable-{Guid.NewGuid()}")
            .Options;

        return new TestContext(options, new AuditableEntityInterceptor(clock.Object, accessor));
    }

    [Fact]
    public async Task Create_Authenticated_SetsCreatedByAndLeavesModifiedByNull()
    {
        // Arrange
        var callerId = Guid.NewGuid();
        var now = new DateTime(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc);
        using var ctx = BuildContext(new FakeUserAccessor { UserId = callerId }, now, out _);
        var widget = new AuditedWidget { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Name = "w" };

        // Act
        ctx.Widgets.Add(widget);
        await ctx.SaveChangesAsync();

        // Assert
        widget.CreatedAt.Should().Be(now);
        widget.CreatedBy.Should().Be(callerId);
        widget.ModifiedBy.Should().BeNull();
        widget.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task Update_AuthenticatedAsSecondUser_SetsModifiedByRefreshesUpdatedAtKeepsCreatedBy()
    {
        // Arrange
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        var accessor = new FakeUserAccessor { UserId = firstUserId };
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupSequence(c => c.UtcNow).Returns(createdAt).Returns(updatedAt);

        var options = new DbContextOptionsBuilder<TestContext>()
            .UseInMemoryDatabase($"auditable-{Guid.NewGuid()}")
            .Options;
        using var ctx = new TestContext(options, new AuditableEntityInterceptor(clock.Object, accessor));

        var widget = new AuditedWidget { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Name = "w" };
        ctx.Widgets.Add(widget);
        await ctx.SaveChangesAsync();

        // Act — second user updates the row.
        accessor.UserId = secondUserId;
        widget.Name = "w2";
        await ctx.SaveChangesAsync();

        // Assert
        widget.CreatedBy.Should().Be(firstUserId);
        widget.ModifiedBy.Should().Be(secondUserId);
        widget.UpdatedAt.Should().Be(updatedAt);
        widget.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public async Task Create_Unauthenticated_LeavesActorsNull()
    {
        // Arrange — anonymous bootstrap writes (sign-in, tenant provisioning)
        // have no authenticated caller.
        var now = new DateTime(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc);
        using var ctx = BuildContext(new FakeUserAccessor { UserId = null }, now, out _);
        var widget = new AuditedWidget { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Name = "w" };

        // Act
        ctx.Widgets.Add(widget);
        await ctx.SaveChangesAsync();

        // Assert
        widget.CreatedBy.Should().BeNull();
        widget.ModifiedBy.Should().BeNull();
        widget.CreatedAt.Should().Be(now);
    }

    [Fact]
    public async Task Update_Unauthenticated_LeavesModifiedByNullAndKeepsCreatedBy()
    {
        // Arrange
        var firstUserId = Guid.NewGuid();
        var accessor = new FakeUserAccessor { UserId = firstUserId };
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc));

        var options = new DbContextOptionsBuilder<TestContext>()
            .UseInMemoryDatabase($"auditable-{Guid.NewGuid()}")
            .Options;
        using var ctx = new TestContext(options, new AuditableEntityInterceptor(clock.Object, accessor));

        var widget = new AuditedWidget { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Name = "w" };
        ctx.Widgets.Add(widget);
        await ctx.SaveChangesAsync();

        // Act — anonymous follow-up write.
        accessor.UserId = null;
        widget.Name = "w2";
        await ctx.SaveChangesAsync();

        // Assert
        widget.CreatedBy.Should().Be(firstUserId);
        widget.ModifiedBy.Should().BeNull();
    }

    [Fact]
    public async Task SaveChanges_DoesNotTouchTenantBinding()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var now = new DateTime(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc);
        using var ctx = BuildContext(new FakeUserAccessor { UserId = callerId }, now, out _);
        var widget = new AuditedWidget { Id = Guid.NewGuid(), TenantId = tenantId, Name = "w" };

        // Act
        ctx.Widgets.Add(widget);
        await ctx.SaveChangesAsync();
        widget.Name = "w2";
        await ctx.SaveChangesAsync();

        // Assert — tenant binding is owned by SaasyEntityInterceptor; the
        // auditable interceptor must leave it unchanged.
        widget.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void AuditEvent_IsTenantScopedButNotAuditable()
    {
        // Assert — slice-2 history rows ride the global query filter but must
        // never be stamped by this interceptor (no recursion).
        typeof(ISaasy).IsAssignableFrom(typeof(AuditEvent)).Should().BeTrue("AuditEvent must be tenant-scoped");
        typeof(IAuditable).IsAssignableFrom(typeof(AuditEvent)).Should().BeFalse("AuditEvent must not be auditable");
        typeof(IEntity).IsAssignableFrom(typeof(AuditEvent)).Should().BeTrue("AuditEvent must have a Guid id");
    }
}
