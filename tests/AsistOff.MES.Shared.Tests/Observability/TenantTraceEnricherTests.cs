using System.Diagnostics;
using AsistOff.MES.Shared.Infrastructure.Observability;
using FluentAssertions;

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
}
