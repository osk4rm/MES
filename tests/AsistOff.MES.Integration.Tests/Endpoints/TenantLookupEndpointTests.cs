using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Proves the anonymous tenant lookup minimization (issue #324) at the HTTP
/// boundary: <c>GET /api/tenants/{id}</c> — anonymous or authenticated —
/// returns the same minimal public projection as registration (id, name,
/// isActive) and never contact e-mail, display name or settings; unknown ids
/// return 404.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class TenantLookupEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task AnonymousGet_ExistingTenant_ReturnsMinimalProjection()
    {
        // Arrange — provision a tenant through the real anonymous endpoint.
        var created = await PostTenantAsync();

        // Act — anonymous lookup of the created tenant id.
        using var client = Fixture.CreateClient();
        var response = await client.GetAsync($"/api/tenants/{created.Id}");

        // Assert — 200 with exactly the minimal keys.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        document.RootElement.EnumerateObject().Select(p => p.Name)
            .Should().BeEquivalentTo("id", "name", "isActive");

        var lookup = await ReadAsync<AnonymousTenantDto>(response);
        lookup.Id.Should().Be(created.Id);
        lookup.Name.Should().Be(created.Name);
        lookup.IsActive.Should().BeTrue();

        var lowered = body.ToLowerInvariant();
        foreach (var fragment in new[] { "contactemail", "displayname", "settings", "country" })
        {
            lowered.Should().NotContain(fragment, $"anonymous lookup must not expose '{fragment}'");
        }
    }

    [Fact]
    public async Task AnonymousGet_UnknownId_Returns404()
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/tenants/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AuthenticatedGet_ExistingTenant_ReturnsSameMinimalProjection()
    {
        // Arrange — provision a tenant, then sign in as the seeded dev admin.
        var created = await PostTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        // Act — authenticated lookup of another tenant's id.
        var response = await client.GetAsync($"/api/tenants/{created.Id}");

        // Assert — auth does not widen the projection either.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        document.RootElement.EnumerateObject().Select(p => p.Name)
            .Should().BeEquivalentTo("id", "name", "isActive");

        var lowered = body.ToLowerInvariant();
        foreach (var fragment in new[] { "contactemail", "displayname", "settings", "country" })
        {
            lowered.Should().NotContain(fragment, $"authenticated lookup must not expose '{fragment}'");
        }
    }

    private async Task<AnonymousTenantDto> PostTenantAsync()
    {
        using var client = Fixture.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var response = await client.PostAsJsonAsync("/api/tenants", new
        {
            name = $"lookup-{suffix}",
            displayName = $"Lookup Tenant {suffix}",
            contactEmail = $"lookup-{suffix}@integration.local",
            settings = string.Empty,
            password = "Passw0rd!",
            confirmPassword = "Passw0rd!",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<AnonymousTenantDto>(response);
    }
}
