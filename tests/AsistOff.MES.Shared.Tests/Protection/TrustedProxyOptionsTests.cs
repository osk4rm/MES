using System.Net;
using System.Text;
using AsistOff.MES.Shared.Infrastructure.Protection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace AsistOff.MES.Shared.Tests.Protection;

/// <summary>
/// Verifies the trusted-proxy allowlist (issue #323): default-deny when
/// unconfigured, explicit proxies/networks honored when configured, and
/// malformed entries ignored without throwing.
/// </summary>
public class TrustedProxyOptionsTests
{
    [Fact]
    public void Defaults_AreEmpty_DefaultDeny()
    {
        // Arrange + Act
        var options = new TrustedProxyOptions();

        // Assert
        options.KnownProxies.Should().BeEmpty();
        options.KnownNetworks.Should().BeEmpty();
        options.GetKnownProxies().Should().BeEmpty();
        options.GetKnownNetworks().Should().BeEmpty();
    }

    [Fact]
    public void GetKnownProxies_ValidEntries_AreParsed()
    {
        // Arrange
        var options = new TrustedProxyOptions { KnownProxies = ["172.18.0.5", " 127.0.0.1 "] };

        // Act
        var proxies = options.GetKnownProxies();

        // Assert
        proxies.Select(p => p.ToString()).Should().BeEquivalentTo("172.18.0.5", "127.0.0.1");
    }

    [Fact]
    public void GetKnownNetworks_ValidCidr_IsParsed()
    {
        // Arrange
        var options = new TrustedProxyOptions { KnownNetworks = ["172.18.0.0/16"] };

        // Act
        var networks = options.GetKnownNetworks();

        // Assert
        networks.Should().ContainSingle().Which.ToString().Should().Be("172.18.0.0/16");
    }

    [Theory]
    [InlineData("not-an-ip")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("999.999.999.999")]
    public void GetKnownProxies_MalformedEntries_AreIgnoredWithoutThrowing(string entry)
    {
        // Arrange
        var options = new TrustedProxyOptions { KnownProxies = [entry] };

        // Act
        var act = () => options.GetKnownProxies();

        // Assert
        act.Should().NotThrow();
        act().Should().BeEmpty();
    }

    [Theory]
    [InlineData("not-a-cidr")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("172.18.0.0/33")]
    public void GetKnownNetworks_MalformedEntries_AreIgnoredWithoutThrowing(string entry)
    {
        // Arrange
        var options = new TrustedProxyOptions { KnownNetworks = [entry] };

        // Act
        var act = () => options.GetKnownNetworks();

        // Assert
        act.Should().NotThrow();
        act().Should().BeEmpty();
    }

    [Fact]
    public void Bind_FromTrustedProxiesSection_AppliesExplicitAllowlist()
    {
        // Arrange
        var json = """
            {
              "TrustedProxies": {
                "KnownProxies": [ "172.18.0.5" ],
                "KnownNetworks": [ "172.18.0.0/16" ]
              }
            }
            """;
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
            .Build();

        // Act
        var options = configuration
            .GetSection(TrustedProxyOptions.SectionName)
            .Get<TrustedProxyOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.KnownProxies.Should().BeEquivalentTo("172.18.0.5");
        options.KnownNetworks.Should().BeEquivalentTo("172.18.0.0/16");
        options.GetKnownProxies().Select(p => p.ToString()).Should().BeEquivalentTo("172.18.0.5");
        options.GetKnownNetworks().Should().ContainSingle();
    }

    [Fact]
    public void Bind_MissingSection_FallsBackToDefaultDeny()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes("{}")))
            .Build();

        // Act
        var options = configuration
            .GetSection(TrustedProxyOptions.SectionName)
            .Get<TrustedProxyOptions>() ?? new TrustedProxyOptions();

        // Assert
        options.KnownProxies.Should().BeEmpty();
        options.KnownNetworks.Should().BeEmpty();
        options.GetKnownProxies().Should().BeEmpty();
        options.GetKnownNetworks().Should().BeEmpty();
    }
}
