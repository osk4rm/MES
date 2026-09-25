using System.Diagnostics;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Infrastructure.Observability;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Shared.Tests.Observability;

/// <summary>
/// Unit tests for the W3C plus X-Correlation-ID propagation bridge and the
/// tenant span enrichment helper (issue #252): valid IDs are kept, invalid
/// ones are replaced with a generated GUID, and an absent tenant adds no tag.
/// </summary>
public class ObservabilityPropagationTests
{
    [Fact]
    public void ResolveCorrelationId_ValidGuid_KeepsVerbatim()
    {
        // Arrange
        var incoming = Guid.NewGuid().ToString();

        // Act
        var result = ObservabilityPropagation.ResolveCorrelationId(incoming);

        // Assert
        result.Should().Be(incoming);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-guid")]
    public void ResolveCorrelationId_AbsentOrInvalid_GeneratesFreshGuid(string? incoming)
    {
        // Act
        var result = ObservabilityPropagation.ResolveCorrelationId(incoming);

        // Assert
        result.Should().NotBe(incoming);
        Guid.TryParse(result, out _).Should().BeTrue();
    }

    [Fact]
    public void AttachCorrelation_NullActivity_ReturnsFalse()
    {
        // Act
        var result = ObservabilityPropagation.AttachCorrelation(null, Guid.NewGuid().ToString());

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AttachCorrelation_BlankValue_ReturnsFalse(string? correlationId)
    {
        // Arrange
        using var activity = new Activity("test").Start();

        // Act
        var result = ObservabilityPropagation.AttachCorrelation(activity, correlationId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void AttachCorrelation_ValidId_SetsBaggageAndTag()
    {
        // Arrange
        using var activity = new Activity("test").Start();
        var correlationId = Guid.NewGuid().ToString();

        // Act
        var result = ObservabilityPropagation.AttachCorrelation(activity, correlationId);

        // Assert
        result.Should().BeTrue();
        activity.GetBaggageItem(ObservabilityPropagation.CorrelationBaggageKey).Should().Be(correlationId);
        activity.GetTagItem(ObservabilityPropagation.CorrelationTagKey).Should().Be(correlationId);
    }

    [Fact]
    public void TryAttachTenant_NullActivity_ReturnsFalse()
    {
        // Arrange
        var accessor = new FakeTenantAccessor(Guid.NewGuid());

        // Act
        var result = ObservabilityPropagation.TryAttachTenant(null, accessor);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TryAttachTenant_NullAccessor_ReturnsFalse()
    {
        // Arrange
        using var activity = new Activity("test").Start();

        // Act
        var result = ObservabilityPropagation.TryAttachTenant(activity, null);

        // Assert
        result.Should().BeFalse();
        activity.GetTagItem(ObservabilityPropagation.TenantTagKey).Should().BeNull();
    }

    [Fact]
    public void TryAttachTenant_AbsentTenant_AddsNoTag()
    {
        // Arrange
        using var activity = new Activity("test").Start();
        var accessor = new FakeTenantAccessor(null);

        // Act
        var result = ObservabilityPropagation.TryAttachTenant(activity, accessor);

        // Assert
        result.Should().BeFalse();
        activity.GetTagItem(ObservabilityPropagation.TenantTagKey).Should().BeNull();
    }

    [Fact]
    public void TryAttachTenant_PresentTenant_SetsTenantIdTag()
    {
        // Arrange
        using var activity = new Activity("test").Start();
        var tenantId = Guid.NewGuid();
        var accessor = new FakeTenantAccessor(tenantId);

        // Act
        var result = ObservabilityPropagation.TryAttachTenant(activity, accessor);

        // Assert
        result.Should().BeTrue();
        activity.GetTagItem(ObservabilityPropagation.TenantTagKey).Should().Be(tenantId.ToString());
    }

    [Theory]
    [InlineData("", false, false)]
    [InlineData("http://localhost:4317", true, false)]
    [InlineData("not-a-uri", true, false)]
    public void AddMesObservability_AnyEndpointConfig_RegistersWithoutThrowing(
        string endpoint, bool hasEndpoint, bool prometheus)
    {
        // Arrange
        var values = new Dictionary<string, string?>
        {
            ["Observability:OtlpEndpoint"] = endpoint,
            ["Observability:PrometheusEnabled"] = prometheus.ToString(),
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        var act = () => services.AddMesObservability(configuration);

        // Assert
        act.Should().NotThrow();
        var provider = services.BuildServiceProvider();
        var bound = configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>()!;
        bound.HasOtlpEndpoint.Should().Be(hasEndpoint);
    }

    private sealed class FakeTenantAccessor(Guid? tenantId) : ICurrentTenantAccessor
    {
        public Guid CurrentTenantId => tenantId ?? Guid.Empty;

        public bool TryGetTenantId(out Guid result)
        {
            if (tenantId is { } id && id != Guid.Empty)
            {
                result = id;
                return true;
            }

            result = Guid.Empty;
            return false;
        }
    }
}
