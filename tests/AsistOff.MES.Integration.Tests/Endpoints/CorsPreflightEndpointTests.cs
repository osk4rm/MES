using AsistOff.MES.Integration.Tests.Infrastructure;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Guards the e2e harness CORS alignment (issue #89): the Development CORS
/// policy must allow both the pinned vite origin (<c>:5173</c>, strictPort)
/// and the fallback origin (<c>:5174</c>) used by manual <c>npm run dev</c>
/// runs outside <c>scripts/e2e/app.ps1</c>.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class CorsPreflightEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    public static TheoryData<string> AllowedOrigins => new()
    {
        "http://localhost:5173",
        "http://localhost:5174",
    };

    [Theory]
    [MemberData(nameof(AllowedOrigins))]
    public async Task Preflight_FromViteOrigin_ReturnsAllowHeader(string origin)
    {
        // Arrange
        using var client = Fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.Headers.Should().ContainSingle(h => h.Key == "Access-Control-Allow-Origin");
        response.Headers.GetValues("Access-Control-Allow-Origin")
            .Should().ContainSingle().Which.Should().Be(origin);
    }

    [Fact]
    public async Task Preflight_FromUnknownOrigin_DoesNotReturnAllowHeader()
    {
        // Arrange
        using var client = Fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", "http://localhost:9999");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.Headers.Should().NotContain(h => h.Key == "Access-Control-Allow-Origin");
    }
}
