using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for Work Center capacity and efficiency
/// (<c>/api/machines</c>): explicit round-trip, defaults when omitted, validation,
/// auth, not-found and cross-tenant isolation.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class MachinesEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/machines";

    [Fact]
    public async Task Create_WithExplicitValues_GetRoundTripsUnchanged()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();

        var create = await client.PostAsJsonAsync(BaseUrl, new
        {
            code,
            name = "Work Center",
            description = (string?)null,
            isActive = true,
            capacity = 2.5m,
            efficiencyFactor = 0.85m
        });

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<MachineDto>(create);
        created.Capacity.Should().Be(2.5m);
        created.EfficiencyFactor.Should().Be(0.85m);

        var get = await client.GetAsync($"{BaseUrl}/{created.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadAsync<MachineDto>(get);
        fetched.Capacity.Should().Be(2.5m);
        fetched.EfficiencyFactor.Should().Be(0.85m);

        var browse = await client.GetAsync($"{BaseUrl}?code={code}");
        browse.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await ReadAsync<PagedResponseDto<MachineDto>>(browse);
        paged.Items.Should().ContainSingle(m => m.Id == created.Id && m.Capacity == 2.5m && m.EfficiencyFactor == 0.85m);
    }

    [Fact]
    public async Task Create_OmittedValues_StoresDefaults()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var create = await client.PostAsJsonAsync(BaseUrl, new
        {
            code = UniqueCode(),
            name = "Work Center",
            description = (string?)null,
            isActive = true
        });

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<MachineDto>(create);
        created.Capacity.Should().Be(1m);
        created.EfficiencyFactor.Should().Be(1.0m);
    }

    [Fact]
    public async Task Update_WithExplicitValues_PersistsAndRoundTrips()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var created = await CreateMachineAsync(client);

        var update = await client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", new
        {
            id = created.Id,
            code = created.Code,
            name = created.Name,
            description = created.Description,
            isActive = created.IsActive,
            departmentId = created.DepartmentId,
            syncId = created.SyncId,
            capacity = 2.5m,
            efficiencyFactor = 0.85m
        });

        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var fetched = await ReadAsync<MachineDto>(await client.GetAsync($"{BaseUrl}/{created.Id}"));
        fetched.Capacity.Should().Be(2.5m);
        fetched.EfficiencyFactor.Should().Be(0.85m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Create_InvalidCapacity_Returns400(decimal capacity)
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            code = UniqueCode(),
            name = "Work Center",
            description = (string?)null,
            isActive = true,
            capacity,
            efficiencyFactor = 1.0m
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    [InlineData(1.5)]
    public async Task Create_EfficiencyOutsideRange_Returns400(decimal efficiencyFactor)
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            code = UniqueCode(),
            name = "Work Center",
            description = (string?)null,
            isActive = true,
            capacity = 1m,
            efficiencyFactor
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_InvalidCapacity_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var created = await CreateMachineAsync(client);

        var response = await client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", new
        {
            id = created.Id,
            code = created.Code,
            name = created.Name,
            description = created.Description,
            isActive = created.IsActive,
            capacity = 0m,
            efficiencyFactor = 1.0m
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_MachineFromAnotherTenant_Returns404()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var foreign = await CreateMachineAsync(otherTenantClient);

        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var get = await client.GetAsync($"{BaseUrl}/{foreign.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var update = await client.PutAsJsonAsync($"{BaseUrl}/{foreign.Id}", new
        {
            id = foreign.Id,
            code = foreign.Code,
            name = foreign.Name,
            description = foreign.Description,
            isActive = foreign.IsActive,
            capacity = 2.0m,
            efficiencyFactor = 0.9m
        });
        update.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<MachineDto> CreateMachineAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            code = UniqueCode(),
            name = "Work Center",
            description = (string?)null,
            isActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<MachineDto>(response);
    }

    private static string UniqueCode() => $"IT-{Guid.NewGuid():N}"[..12];
}
