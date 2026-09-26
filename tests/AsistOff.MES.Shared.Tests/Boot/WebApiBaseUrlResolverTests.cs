using AsistOff.MES.Shared.Infrastructure.Boot;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Boot;

/// <summary>
/// Unit tests for the web API base URL precedence (issue #271): the
/// <c>API_BASE_URL</c> environment value wins over the build-time fallback,
/// which wins over the documented default; an emptied configuration fails
/// fast with a named message. Mirrors the contract implemented by
/// <c>AsistOff.MES.Web/docker-entrypoint.sh</c> and
/// <c>src/services/apiBaseUrl.ts</c>.
/// </summary>
public sealed class WebApiBaseUrlResolverTests
{
    [Fact]
    public void Resolve_EnvironmentValue_WinsOverFallback()
    {
        // Arrange
        const string environment = "https://mes.example.com";
        const string fallback = "http://localhost:8080";

        // Act
        var resolved = WebApiBaseUrlResolver.Resolve(environment, fallback);

        // Assert
        resolved.Should().Be(environment);
    }

    [Fact]
    public void Resolve_MissingEnvironmentValue_FallsBackToDefault()
    {
        // Arrange
        string? environment = null;

        // Act
        var resolved = WebApiBaseUrlResolver.Resolve(environment);

        // Assert
        resolved.Should().Be(WebApiBaseUrlResolver.DefaultApiBaseUrl);
    }

    [Fact]
    public void Resolve_BuildTimeFallback_UsedWhenEnvironmentMissing()
    {
        // Arrange — the compose build arg VITE_API_BASE_URL passthrough.

        // Act
        var resolved = WebApiBaseUrlResolver.Resolve(null, "http://backend:8080");

        // Assert
        resolved.Should().Be("http://backend:8080");
    }

    [Fact]
    public void Resolve_WhitespaceValues_AreTrimmed()
    {
        // Arrange

        // Act
        var resolved = WebApiBaseUrlResolver.Resolve("  https://mes.example.com  ", "http://localhost:8080");

        // Assert
        resolved.Should().Be("https://mes.example.com");
    }

    [Fact]
    public void Resolve_NoUsableValue_ThrowsNamingVariable()
    {
        // Arrange — operator emptied the variable and removed the default.

        // Act
        var act = () => WebApiBaseUrlResolver.Resolve(string.Empty, "   ");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*API_BASE_URL*not configured*");
    }
}
