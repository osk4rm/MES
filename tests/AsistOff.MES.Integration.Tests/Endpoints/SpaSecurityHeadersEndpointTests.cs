using System.Net;
using AsistOff.MES.Integration.Tests.Infrastructure;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped contract for the SPA security-headers split (issue #340).
/// nginx emits the SPA header set (X-Frame-Options DENY plus the Vite-SPA
/// CSP) on static responses, while proxied <c>/api</c> responses must keep
/// exactly the backend <c>SecurityHeadersMiddleware</c> values with no
/// duplication and no leakage of the nginx-only headers. nginx itself is not
/// part of the test host, so these tests pin the backend half of that
/// contract over HTTP: single-valued backend headers, the backend
/// <c>frame-ancestors 'none'</c> policy (not the SPA <c>'self'</c> value),
/// and no <c>X-Frame-Options</c> on API responses.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class SpaSecurityHeadersEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task HealthResponse_CarriesBackendHeadersExactlyOnce_WithoutSpaOnlyHeaders()
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertBackendHeadersWithoutDuplication(response);
    }

    [Fact]
    public async Task AuthenticatedApiResponse_KeepsBackendHeaders_WithoutDuplication()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/api/reason-codes");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertBackendHeadersWithoutDuplication(response);
    }

    /// <summary>
    /// The backend half of the issue #340 no-duplication contract: every
    /// header the backend owns is present exactly once with the backend
    /// value, and the nginx-only SPA headers are absent so a same-origin
    /// proxy cannot duplicate or override them.
    /// </summary>
    private static void AssertBackendHeadersWithoutDuplication(HttpResponseMessage response)
    {
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle()
            .Which.Should().Be("nosniff");
        response.Headers.GetValues("Referrer-Policy").Should().ContainSingle()
            .Which.Should().Be("no-referrer");
        response.Headers.GetValues("Content-Security-Policy").Should().ContainSingle()
            .Which.Should().Contain("frame-ancestors 'none'");
        response.Headers.Contains("X-Frame-Options").Should().BeFalse(
            "X-Frame-Options DENY is nginx-only for SPA responses; /api keeps the backend CSP");
    }
}
