using AsistOff.MES.IntegrationTests.Infrastructure;

namespace AsistOff.MES.IntegrationTests.Tenants;

public sealed class TenantsEndpointsTests : IntegrationTestBase
{
    public TenantsEndpointsTests(MesApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Create_then_get_returns_the_tenant()
    {
        var name = "tenant-" + Guid.NewGuid().ToString("N")[..8];
        var createResponse = await AnonymousClient.PostAsJsonAsync("/api/tenants", new
        {
            Name = name,
            DisplayName = "Display " + name,
            ContactEmail = $"admin@{name}.local",
            Settings = string.Empty,
            Password = "Passw0rd!",
            ConfirmPassword = "Passw0rd!",
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = await createResponse.Content.ReadFromJsonAsync<Guid>(Json);
        id.Should().NotBeEmpty();

        var getResponse = await AnonymousClient.GetAsync($"/api/tenants/{id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await getResponse.Content.ReadFromJsonAsync<TenantPayload>(Json);
        payload.Should().NotBeNull();
        payload!.Id.Should().Be(id);
        payload.Name.Should().Be(name);
    }

    [Fact]
    public async Task Create_returns_validation_error_when_payload_is_invalid()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/tenants", new
        {
            Name = "",
            DisplayName = "x",
            ContactEmail = "not-an-email",
            Settings = string.Empty,
            Password = "short",
            ConfirmPassword = "different",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record TenantPayload(Guid Id, string Name, string DisplayName, string ContactEmail);
}
