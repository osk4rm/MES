using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>/api/scrap-events</c>. They exercise
/// the full request pipeline: authentication, tenant resolution, MediatR handler,
/// EF Core persistence and the global exception handler - against a real
/// PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ScrapEventsEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/scrap-events";

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
        var machineId = Guid.NewGuid();
        var reasonCodeId = Guid.NewGuid();

        var createResponse = await client.PostAsJsonAsync(BaseUrl, CreatePayload(machineId, reasonCodeId, 4.5m));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ScrapEventDto>(createResponse);
        created.Id.Should().NotBeEmpty();
        created.MachineId.Should().Be(machineId);
        created.ReasonCodeId.Should().Be(reasonCodeId);
        created.Quantity.Should().Be(4.5m);

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadAsync<ScrapEventDto>(getResponse);
        fetched.Id.Should().Be(created.Id);
        fetched.Quantity.Should().Be(4.5m);
        fetched.ReasonCodeId.Should().Be(reasonCodeId);
        fetched.MachineId.Should().Be(machineId);
    }

    [Fact]
    public async Task Create_WithZeroQuantity_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(BaseUrl, CreatePayload(quantity: 0m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithFutureReportedAt_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            BaseUrl, CreatePayload(reportedAt: DateTime.UtcNow.AddHours(2)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithProductionOrderId_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            BaseUrl, CreatePayload(productionOrderId: Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Browse_WithMachineFilter_ReturnsMatchingItem()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = Guid.NewGuid();
        var create = await client.PostAsJsonAsync(BaseUrl, CreatePayload(machineId: machineId));
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.GetAsync($"{BaseUrl}?machineId={machineId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<ScrapEventDto>>(response);
        page.Items.Should().ContainSingle(item => item.MachineId == machineId);
    }

    [Fact]
    public async Task Get_WithUnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_PersistsChange()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var create = await client.PostAsJsonAsync(BaseUrl, CreatePayload());
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ScrapEventDto>(create);
        var newReasonCodeId = Guid.NewGuid();

        var updateResponse = await client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", new
        {
            id = created.Id,
            reasonCodeId = newReasonCodeId,
            quantity = 9.25m,
            notes = "reclassified"
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await client.GetAsync($"{BaseUrl}/{created.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadAsync<ScrapEventDto>(get);
        fetched.Quantity.Should().Be(9.25m);
        fetched.ReasonCodeId.Should().Be(newReasonCodeId);
        fetched.Notes.Should().Be("reclassified");
        fetched.MachineId.Should().Be(created.MachineId);
        fetched.ReportedAt.Should().BeCloseTo(created.ReportedAt, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Update_WithUnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var id = Guid.NewGuid();

        var response = await client.PutAsJsonAsync($"{BaseUrl}/{id}", new
        {
            id,
            reasonCodeId = Guid.NewGuid(),
            quantity = 1m,
            notes = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_RemovesEvent()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var create = await client.PostAsJsonAsync(BaseUrl, CreatePayload());
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ScrapEventDto>(create);

        var deleteResponse = await client.DeleteAsync($"{BaseUrl}/{created.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await client.GetAsync($"{BaseUrl}/{created.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_WithUnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.DeleteAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_RecordFromAnotherTenant_Returns404()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var create = await otherTenantClient.PostAsJsonAsync(BaseUrl, CreatePayload());
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ScrapEventDto>(create);

        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var response = await devClient.GetAsync($"{BaseUrl}/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static object CreatePayload(
        Guid? machineId = null,
        Guid? reasonCodeId = null,
        decimal quantity = 2m,
        DateTime? reportedAt = null,
        Guid? productionOrderId = null) => new
    {
        machineId = machineId ?? Guid.NewGuid(),
        reasonCodeId = reasonCodeId ?? Guid.NewGuid(),
        quantity,
        reportedAt = reportedAt ?? DateTime.UtcNow.AddMinutes(-10),
        notes = (string?)"integration test",
        reportedByOperatorId = (Guid?)null,
        productionOrderId
    };
}
