using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>/api/shifts</c>. They exercise
/// the full request pipeline: authentication, tenant resolution, MediatR
/// handler, EF Core persistence and the global exception handler - against a
/// real PostgreSQL database.
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
        var payload = new
        {
            code,
            name = "Morning",
            description = "First shift",
            startTime = "06:00:00",
            endTime = "14:00:00",
            isActive = true
        };

        var createResponse = await client.PostAsJsonAsync(BaseUrl, payload);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ShiftDto>(createResponse);
        created.Code.Should().Be(code);
        created.Id.Should().NotBeEmpty();
        created.StartTime.Should().Be(new TimeOnly(6, 0));
        created.EndTime.Should().Be(new TimeOnly(14, 0));

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadAsync<ShiftDto>(getResponse);
        fetched.Id.Should().Be(created.Id);
        fetched.Name.Should().Be("Morning");
    }

    [Fact]
    public async Task Create_OvernightWindow_IsAccepted()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var payload = new
        {
            code = UniqueCode(),
            name = "Night",
            description = (string?)null,
            startTime = "22:00:00",
            endTime = "06:00:00",
            isActive = true
        };

        var response = await client.PostAsJsonAsync(BaseUrl, payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_WithDuplicateCode_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();
        var payload = new { code, name = "Duplicate", description = (string?)null, startTime = "06:00:00", endTime = "14:00:00", isActive = true };

        var first = await client.PostAsJsonAsync(BaseUrl, payload);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync(BaseUrl, payload);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_WithEmptyCode_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var payload = new { code = "", name = "No code", description = (string?)null, startTime = "06:00:00", endTime = "14:00:00", isActive = true };

        var response = await client.PostAsJsonAsync(BaseUrl, payload);

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
    public async Task Browse_WithCodeFilter_ReturnsMatchingItem()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();
        await client.PostAsJsonAsync(BaseUrl, new { code, name = "Filtered", description = (string?)null, startTime = "06:00:00", endTime = "14:00:00", isActive = true });

        var response = await client.GetAsync($"{BaseUrl}?code={code}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<ShiftDto>>(response);
        page.Items.Should().ContainSingle(item => item.Code == code);
    }

    [Fact]
    public async Task Update_AndDelete_RoundTrip()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();
        var created = await ReadAsync<ShiftDto>(await client.PostAsJsonAsync(BaseUrl,
            new { code, name = "Before", description = (string?)null, startTime = "06:00:00", endTime = "14:00:00", isActive = true }));

        var update = await client.PutAsJsonAsync($"{BaseUrl}/{created.Id}",
            new { id = created.Id, code, name = "After", description = (string?)null, startTime = "14:00:00", endTime = "22:00:00", isActive = false });

        update.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var fetched = await ReadAsync<ShiftDto>(await client.GetAsync($"{BaseUrl}/{created.Id}"));
        fetched.Name.Should().Be("After");
        fetched.IsActive.Should().BeFalse();

        var delete = await client.DeleteAsync($"{BaseUrl}/{created.Id}");

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"{BaseUrl}/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Shifts_AreIsolatedPerTenant()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();
        await client.PostAsJsonAsync(BaseUrl,
            new { code, name = "Tenant A", description = (string?)null, startTime = "06:00:00", endTime = "14:00:00", isActive = true });

        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherClient = await Fixture.CreateAuthenticatedClientAsync(email, password);

        var response = await otherClient.GetAsync($"{BaseUrl}?code={code}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<ShiftDto>>(response);
        page.Items.Should().BeEmpty();
    }

    private static string UniqueCode() => $"IT-{Guid.NewGuid():N}"[..12];
}
