using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>/api/reason-codes</c>. They exercise
/// the full request pipeline: authentication, tenant resolution, validation
/// behavior, MediatR handler, EF Core persistence and the global exception
/// handler - against a real PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ReasonCodesEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/reason-codes";

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
            name = "Breakdown",
            description = "Machine breakdown",
            category = 1,
            isActive = true,
            sortIndex = 0
        };

        var createResponse = await client.PostAsJsonAsync(BaseUrl, payload);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ReasonCodeDto>(createResponse);
        created.Code.Should().Be(code);
        created.Id.Should().NotBeEmpty();

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadAsync<ReasonCodeDto>(getResponse);
        fetched.Id.Should().Be(created.Id);
        fetched.Name.Should().Be("Breakdown");
    }

    [Fact]
    public async Task Create_WithDuplicateCode_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();
        var payload = new { code, name = "Duplicate", description = (string?)null, category = 1, isActive = true, sortIndex = 0 };

        var first = await client.PostAsJsonAsync(BaseUrl, payload);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync(BaseUrl, payload);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_WithEmptyCode_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var payload = new { code = "", name = "No code", description = (string?)null, category = 1, isActive = true, sortIndex = 0 };

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
        await client.PostAsJsonAsync(BaseUrl, new { code, name = "Filtered", description = (string?)null, category = 2, isActive = true, sortIndex = 0 });

        var response = await client.GetAsync($"{BaseUrl}?code={code}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<ReasonCodeDto>>(response);
        page.Items.Should().ContainSingle(item => item.Code == code);
    }

    private static string UniqueCode() => $"IT-{Guid.NewGuid():N}"[..12];
}
