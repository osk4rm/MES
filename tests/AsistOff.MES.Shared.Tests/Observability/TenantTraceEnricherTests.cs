using System.Diagnostics;
using AsistOff.MES.Shared.Infrastructure.Observability;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace AsistOff.MES.Shared.Tests.Observability;

/// <summary>
/// Verifies tenant span enrichment: a present tenant id becomes a
/// <c>tenant_id</c> span tag, an absent tenant (empty GUID) adds no tag.
/// </summary>
public class TenantTraceEnricherTests
{
    [Fact]
    public void TryEnrichWithTenant_PresentTenant_SetsTenantTag()
    {
        // Arrange
        using var activity = new Activity("test-operation").Start();
        var tenantId = Guid.NewGuid();

        // Act
        var enriched = TenantTraceEnricher.TryEnrichWithTenant(activity, tenantId);

        // Assert
        enriched.Should().BeTrue();
        activity.GetTagItem(TenantTraceEnricher.TenantTagKey).Should().Be(tenantId.ToString());
    }

    [Fact]
    public void TryEnrichWithTenant_AbsentTenant_AddsNoTag()
    {
        // Arrange
        using var activity = new Activity("test-operation").Start();

        // Act
        var enriched = TenantTraceEnricher.TryEnrichWithTenant(activity, Guid.Empty);

        // Assert
        enriched.Should().BeFalse();
        activity.GetTagItem(TenantTraceEnricher.TenantTagKey).Should().BeNull();
    }

    [Fact]
    public void TryEnrichWithTenant_NullActivity_ReturnsFalse()
    {
        // Act
        var enriched = TenantTraceEnricher.TryEnrichWithTenant(null, Guid.NewGuid());

        // Assert
        enriched.Should().BeFalse();
    }

    [Fact]
    public void TryReadTenantId_StashedGuid_ReturnsTenant()
    {
        // Arrange - the stash TenantTraceContextMiddleware writes while the
        // request scope is alive.
        var tenantId = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Items[TenantTraceEnricher.TenantItemKey] = tenantId;

        // Act
        var read = TenantTraceEnricher.TryReadTenantId(context, out var resolved);

        // Assert
        read.Should().BeTrue();
        resolved.Should().Be(tenantId);
    }

    [Fact]
    public void TryReadTenantId_MissingOrEmpty_ReturnsFalse()
    {
        // Arrange
        var missing = new DefaultHttpContext();
        var empty = new DefaultHttpContext();
        empty.Items[TenantTraceEnricher.TenantItemKey] = Guid.Empty;

        // Act + Assert
        TenantTraceEnricher.TryReadTenantId(missing, out _).Should().BeFalse();
        TenantTraceEnricher.TryReadTenantId(empty, out _).Should().BeFalse();
        TenantTraceEnricher.TryReadTenantId(null, out _).Should().BeFalse();
    }
}
