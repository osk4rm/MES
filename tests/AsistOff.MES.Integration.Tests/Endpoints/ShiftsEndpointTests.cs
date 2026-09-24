using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>/api/shifts</c> - the named
/// working-time windows used by Work Center calendars. They drive the full
/// pipeline (auth, tenant resolution, MediatR, EF Core, exception handler)
/// against a real PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ShiftsEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/shifts";

    [Fact]
    public async Task Browse_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_ReturnsCreated_AndIsRetrievableById()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();

        var createResponse = await client.PostAsJsonAsync(BaseUrl, new
        {
            code,
            name = "Morning",
            description = "06:00-14:00",
            startTime = "06:00:00",
            endTime = "14:00:00",
            isActive = true
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ShiftDto>(createResponse);
        created.Id.Should().NotBeEmpty();
        created.Code.Should().Be(code);
        created.StartTime.Should().Be("06:00:00");

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadAsync<ShiftDto>(getResponse);
        fetched.Id.Should().Be(created.Id);
        fetched.Name.Should().Be("Morning");
        fetched.EndTime.Should().Be("14:00:00");
    }

    [Fact]
    public async Task Create_OvernightWindow_IsAccepted()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            code,
            name = "Night",
            description = (string?)null,
            startTime = "22:00:00",
            endTime = "06:00:00",
            isActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ShiftDto>(response);
        created.StartTime.Should().Be("22:00:00");
        created.EndTime.Should().Be("06:00:00");
    }

    [Fact]
    public async Task Create_WithDuplicateCode_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();
        var payload = new
        {
            code,
            name = "Duplicate",
            description = (string?)null,
            startTime = "06:00:00",
            endTime = "14:00:00",
            isActive = true
        };

        var first = await client.PostAsJsonAsync(BaseUrl, payload);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync(BaseUrl, payload);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_WithEmptyCode_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            code = "",
            name = "No code",
            description = (string?)null,
            startTime = "06:00:00",
            endTime = "14:00:00",
            isActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_WithUnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ChangesWindow_AndReturns204()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var created = await CreateShiftAsync(client);

        var response = await client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", new
        {
            id = created.Id,
            code = created.Code,
            name = "Evening",
            description = (string?)null,
            startTime = "14:00:00",
            endTime = "22:00:00",
            isActive = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var fetched = await ReadAsync<ShiftDto>(await client.GetAsync($"{BaseUrl}/{created.Id}"));
        fetched.Name.Should().Be("Evening");
        fetched.IsActive.Should().BeFalse();
        fetched.StartTime.Should().Be("14:00:00");
    }

    [Fact]
    public async Task Update_WithUnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var unknownId = Guid.NewGuid();

        var response = await client.PutAsJsonAsync($"{BaseUrl}/{unknownId}", new
        {
            id = unknownId,
            code = UniqueCode(),
            name = "Ghost",
            description = (string?)null,
            startTime = "06:00:00",
            endTime = "14:00:00",
            isActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_RemovesShift()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var created = await CreateShiftAsync(client);

        var deleteResponse = await client.DeleteAsync($"{BaseUrl}/{created.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Browse_WithCodeAndActiveFilters_ReturnsMatchingItem()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();
        await client.PostAsJsonAsync(BaseUrl, new
        {
            code,
            name = "Filtered",
            description = (string?)null,
            startTime = "06:00:00",
            endTime = "14:00:00",
            isActive = true
        });

        var response = await client.GetAsync($"{BaseUrl}?code={code}&isActive=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<ShiftDto>>(response);
        page.Items.Should().ContainSingle(item => item.Code == code);
    }

    [Fact]
    public async Task Browse_ShiftCreatedInAnotherTenant_IsNotVisible()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var code = UniqueCode();
        await CreateShiftAsync(otherTenantClient, code);

        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var response = await devClient.GetAsync($"{BaseUrl}?code={code}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<ShiftDto>>(response);
        page.Items.Should().BeEmpty();
    }

    private static async Task<ShiftDto> CreateShiftAsync(HttpClient client, string? code = null)
    {
        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            code = code ?? UniqueCode(),
            name = "Morning",
            description = (string?)null,
            startTime = "06:00:00",
            endTime = "14:00:00",
            isActive = true
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<ShiftDto>(response);
    }

    private static string UniqueCode() => $"IT-{Guid.NewGuid():N}"[..12];
}
