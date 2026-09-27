using AsistOff.MES.Gateway.Protection;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Protection;

/// <summary>
/// Locks the Development CORS hardening (issue #369): the credentialed
/// fallback accepts only loopback origins, rejects an arbitrary evil origin,
/// and Production with no configured origins still fails fast.
/// </summary>
public class DevCorsPolicyTests
{
    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("http://localhost:4200")]
    [InlineData("https://localhost:3000")]
    [InlineData("http://127.0.0.1:3000")]
    [InlineData("http://127.0.0.1:8080")]
    [InlineData("http://[::1]:8080")]
    [InlineData("https://127.0.0.1")]
    public void IsLoopbackOrigin_LoopbackOrigin_ReturnsTrue(string origin)
    {
        // Arrange + Act
        var actual = DevCorsPolicy.IsLoopbackOrigin(origin);

        // Assert
        actual.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://evil.test")]
    [InlineData("http://evil.test")]
    [InlineData("https://mes.example.com")]
    [InlineData("http://192.168.1.10:8080")]
    [InlineData("http://10.0.0.5")]
    [InlineData("http://localhost.evil.test")]
    [InlineData("http://evil-localhost")]
    [InlineData("ftp://localhost/file")]
    [InlineData("not-a-uri")]
    [InlineData("")]
    [InlineData("   ")]
    public void IsLoopbackOrigin_NonLoopbackOrMalformed_ReturnsFalse(string origin)
    {
        // Arrange + Act
        var actual = DevCorsPolicy.IsLoopbackOrigin(origin);

        // Assert
        actual.Should().BeFalse();
    }

    [Fact]
    public void IsLoopbackOrigin_Null_ReturnsFalse()
    {
        // Arrange + Act
        var actual = DevCorsPolicy.IsLoopbackOrigin(null!);

        // Assert
        actual.Should().BeFalse();
    }

    [Fact]
    public void EnsureConfigured_ConfiguredOrigins_ReturnsThemUnchanged()
    {
        // Arrange
        var configured = new[] { "https://mes.example.com" };

        // Act
        var dev = DevCorsPolicy.EnsureConfigured(configured, isDevelopment: true);
        var prod = DevCorsPolicy.EnsureConfigured(configured, isDevelopment: false);

        // Assert
        dev.Should().BeEquivalentTo(configured);
        prod.Should().BeEquivalentTo(configured);
    }

    [Fact]
    public void EnsureConfigured_EmptyInDevelopment_ReturnsEmptyForLoopbackFallback()
    {
        // Arrange + Act
        var actual = DevCorsPolicy.EnsureConfigured([], isDevelopment: true);

        // Assert
        actual.Should().BeEmpty();
    }

    [Fact]
    public void EnsureConfigured_EmptyOutsideDevelopment_Throws()
    {
        // Arrange + Act
        var act = () => DevCorsPolicy.EnsureConfigured([], isDevelopment: false);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cors:allowedOrigins*");
    }

    [Fact]
    public void EnsureConfigured_NullOutsideDevelopment_Throws()
    {
        // Arrange + Act
        var act = () => DevCorsPolicy.EnsureConfigured(null, isDevelopment: false);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cors:allowedOrigins*");
    }
}
