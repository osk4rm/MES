using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Infrastructure.Interceptors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AsistOff.MES.Shared.Tests.Multitenancy;

public class SaasyEntityInterceptorTests
{
    private sealed class Widget : ISaasy, IEntity
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestContext : DbContext
    {
        private readonly SaasyEntityInterceptor _interceptor;

        public DbSet<Widget> Widgets => Set<Widget>();

        public TestContext(DbContextOptions<TestContext> options, SaasyEntityInterceptor interceptor)
            : base(options)
        {
            _interceptor = interceptor;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(_interceptor);
        }
    }

    private static TestContext BuildContext(Guid currentTenant, bool hasTenant = true)
    {
        var accessor = new Mock<ICurrentTenantAccessor>();
        accessor.Setup(a => a.TryGetTenantId(out currentTenant)).Returns(hasTenant);
        accessor.SetupGet(a => a.CurrentTenantId).Returns(hasTenant ? currentTenant : Guid.Empty);

        var options = new DbContextOptionsBuilder<TestContext>()
            .UseInMemoryDatabase($"interceptor-{Guid.NewGuid()}")
            .Options;

        return new TestContext(options, new SaasyEntityInterceptor(accessor.Object));
    }

    [Fact]
    public async Task Assigns_current_tenant_when_inserting_entity_without_tenantId()
    {
        var tenantId = Guid.NewGuid();
        using var ctx = BuildContext(tenantId);

        var widget = new Widget { Id = Guid.NewGuid(), Name = "gadget", TenantId = Guid.Empty };
        ctx.Widgets.Add(widget);
        await ctx.SaveChangesAsync();

        widget.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task Rejects_insert_with_foreign_tenantId()
    {
        var tenantId = Guid.NewGuid();
        using var ctx = BuildContext(tenantId);

        var widget = new Widget { Id = Guid.NewGuid(), Name = "gadget", TenantId = Guid.NewGuid() };
        ctx.Widgets.Add(widget);

        var act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(e => e.Message.Contains("Cross‑tenant write"));
    }

    [Fact]
    public async Task Rejects_modification_of_tenantId()
    {
        var tenantId = Guid.NewGuid();
        using var ctx = BuildContext(tenantId);

        var widget = new Widget { Id = Guid.NewGuid(), Name = "gadget", TenantId = Guid.Empty };
        ctx.Widgets.Add(widget);
        await ctx.SaveChangesAsync();

        widget.TenantId = Guid.NewGuid();
        var act = () => ctx.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(e => e.Message.Contains("TenantId") && e.Message.Contains("cannot be changed"));
    }

    [Fact]
    public async Task Throws_when_no_ambient_tenant_and_entity_has_no_tenantId()
    {
        using var ctx = BuildContext(Guid.Empty, hasTenant: false);

        var widget = new Widget { Id = Guid.NewGuid(), Name = "gadget", TenantId = Guid.Empty };
        ctx.Widgets.Add(widget);

        var act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(e => e.Message.Contains("without an ambient tenant"));
    }
}
