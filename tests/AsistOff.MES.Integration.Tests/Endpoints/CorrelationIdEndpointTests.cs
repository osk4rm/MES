using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint integration tests for end-to-end correlation ID propagation
/// (issue #251): the Gateway echoes the effective <c>X-Correlation-ID</c> on
/// every response and every error envelope from the global exception handler
/// carries a matching <c>traceId</c>. Auth and tenant behavior must not
/// regress with the middleware enabled.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class CorrelationIdEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string HeaderName = "X-Correlation-ID";

    [Fact]
    public async Task Get_WithoutHeader_ReturnsGeneratedGuid()
    {
        // Arrange - anonymous client, no correlation header.
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var echoed = GetHeader(response);
        echoed.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(echoed!, out _).Should().BeTrue();
    }

    [Fact]
    public async Task Get_WithValidHeader_EchoesVerbatim_OnSuccess()
    {
        // Arrange
        using var client = Fixture.CreateClient();
        var correlationId = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(HeaderName, correlationId);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        GetHeader(response).Should().Be(correlationId);
    }

    [Fact]
    public async Task Get_WithInvalidHeader_ReplacesWithFreshGuid()
    {
        // Arrange
        using var client = Fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(HeaderName, "not-a-guid");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var echoed = GetHeader(response);
        echoed.Should().NotBe("not-a-guid");
        Guid.TryParse(echoed!, out _).Should().BeTrue();
    }

    [Fact]
    public async Task NotFoundError_CarriesTraceId_EqualToEchoedHeader()
    {
        // Arrange - authenticated read of a missing Work Center -> 404 from
        // the global exception handler.
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var correlationId = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/machines/{Guid.NewGuid()}");
        request.Headers.Add(HeaderName, correlationId);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.GetValues(HeaderName).Single().Should().Be(correlationId);
        (await ReadTraceIdAsync(response)).Should().Be(correlationId);
    }

    [Fact]
    public async Task UnauthorizedError_CarriesTraceId_EqualToEchoedHeader()
    {
        // Arrange - anonymous sign-in with bad credentials -> 401 from the
        // global exception handler; no header sent so one is generated.
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new
        {
            email = $"nobody-{Guid.NewGuid():N}@integration.local",
            password = "not-the-password"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var echoed = GetHeader(response);
        Guid.TryParse(echoed!, out _).Should().BeTrue();
        (await ReadTraceIdAsync(response)).Should().Be(echoed);
    }

    [Fact]
    public async Task AuthenticatedRead_WithFixedHeader_EchoesIt_OnSuccess()
    {
        // Arrange - tenant read keeps working with the middleware enabled.
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var correlationId = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/machines?page=1&pageSize=1");
        request.Headers.Add(HeaderName, correlationId);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues(HeaderName).Single().Should().Be(correlationId);
    }

    private static string? GetHeader(HttpResponseMessage response)
        => response.Headers.TryGetValues(HeaderName, out var values) ? values.SingleOrDefault() : null;

    private static async Task<string?> ReadTraceIdAsync(HttpResponseMessage response)
    {
        using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return payload.RootElement.TryGetProperty("traceId", out var traceId)
            ? traceId.GetString()
            : null;
    }
}
