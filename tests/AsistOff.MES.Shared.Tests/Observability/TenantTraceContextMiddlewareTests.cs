using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Infrastructure.Observability;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Shared.Tests.Observability;

/// <summary>
/// Verifies the tenant stash for issue #252 trace enrichment: after the
/// downstream pipeline completes, the resolved tenant id is available on
/// <c>HttpContext.Items</c> for the OTel response callback (which cannot
/// resolve services itself). Anonymous/unresolved requests stash nothing and
/// never throw.
/// </summary>
public sealed class TenantTraceContextMiddlewareTests
{
    [Fact]
    public async Task Invoke_WithResolvedTenant_StashesTenantIdOnItems()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.RequestServices = BuildServices(new StubTenantAccessor(tenantId));
        var invoked = false;
        var middleware = new TenantTraceContextMiddleware(_ =>
        {
            invoked = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        invoked.Should().BeTrue();
        context.Items.TryGetValue(TenantTraceEnricher.TenantItemKey, out var stored).Should().BeTrue();
        stored.Should().Be(tenantId);
    }

    [Fact]
    public async Task Invoke_WithoutResolvedTenant_StashesNothing()
    {
        // Arrange - no accessor registered (e.g. anonymous infrastructure
        // endpoints before tenant resolution).
        var context = new DefaultHttpContext();
        context.RequestServices = new ServiceCollection().BuildServiceProvider();
        var middleware = new TenantTraceContextMiddleware(_ => Task.CompletedTask);

        // Act
        var act = () => middleware.InvokeAsync(context);

        // Assert
        await act.Should().NotThrowAsync();
        context.Items.ContainsKey(TenantTraceEnricher.TenantItemKey).Should().BeFalse();
    }

    [Fact]
    public async Task Invoke_WithEmptyTenant_StashesNothing()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.RequestServices = BuildServices(new StubTenantAccessor(Guid.Empty));
        var middleware = new TenantTraceContextMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Items.ContainsKey(TenantTraceEnricher.TenantItemKey).Should().BeFalse();
    }

    private static IServiceProvider BuildServices(ICurrentTenantAccessor accessor) =>
        new ServiceCollection().AddSingleton(accessor).BuildServiceProvider();

    private sealed class StubTenantAccessor(Guid tenantId) : ICurrentTenantAccessor
    {
        public Guid CurrentTenantId => tenantId;

        public bool TryGetTenantId(out Guid id)
        {
            id = tenantId;
            return tenantId != Guid.Empty;
        }
    }
}
