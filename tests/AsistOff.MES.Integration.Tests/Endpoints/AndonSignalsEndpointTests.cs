using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>/api/andon-signals</c>. They exercise
/// the full request pipeline: authentication, tenant resolution, MediatR handler,
/// EF Core persistence and the global exception handler - against a real
/// PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class AndonSignalsEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/andon-signals";

    private static object RaisePayload(Guid machineId, DateTime? raisedAt = null) => new
    {
        machineId,
        category = 1,
        reasonCodeId = (Guid?)null,
        raisedAt = raisedAt ?? DateTime.UtcNow.AddMinutes(-5),
        notes = "Belt jammed",
        raisedByOperatorId = (Guid?)null,
        productionOrderId = (Guid?)null
    };

    private async Task<AndonSignalDto> RaiseAsync(HttpClient client, Guid? machineId = null, DateTime? raisedAt = null)
    {
        var response = await client.PostAsJsonAsync(BaseUrl, RaisePayload(machineId ?? Guid.NewGuid(), raisedAt));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<AndonSignalDto>(response);
    }

    [Fact]
    public async Task Browse_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Raise_ReturnsCreated_AndIsRetrievableById()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = Guid.NewGuid();

        var createResponse = await client.PostAsJsonAsync(BaseUrl, RaisePayload(machineId));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<AndonSignalDto>(createResponse);
        created.MachineId.Should().Be(machineId);
        created.Status.Should().Be((short)1);

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadAsync<AndonSignalDto>(getResponse);
        fetched.Id.Should().Be(created.Id);
        fetched.Category.Should().Be((short)1);
        fetched.Notes.Should().Be("Belt jammed");
    }

    [Fact]
    public async Task Raise_WithFutureRaisedAt_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(BaseUrl, RaisePayload(Guid.NewGuid(), DateTime.UtcNow.AddHours(1)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Raise_WithProductionOrderId_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            machineId = Guid.NewGuid(),
            category = 1,
            reasonCodeId = (Guid?)null,
            raisedAt = DateTime.UtcNow.AddMinutes(-5),
            notes = (string?)null,
            raisedByOperatorId = (Guid?)null,
            productionOrderId = (Guid?)Guid.NewGuid()
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Raise_SecondActiveSignalOnSameWorkCenter_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = Guid.NewGuid();
        await RaiseAsync(client, machineId);

        var response = await client.PostAsJsonAsync(BaseUrl, RaisePayload(machineId));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Acknowledge_Returns200_AndSecondAcknowledge_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var created = await RaiseAsync(client);

        var first = await client.PostAsJsonAsync($"{BaseUrl}/{created.Id}/acknowledge", new { });

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var acknowledged = await ReadAsync<AndonSignalDto>(first);
        acknowledged.Status.Should().Be((short)2);
        acknowledged.AcknowledgedAt.Should().NotBeNull();

        var second = await client.PostAsJsonAsync($"{BaseUrl}/{created.Id}/acknowledge", new { });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Resolve_Returns200_AndPersistsChange()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var created = await RaiseAsync(client);

        var resolve = await client.PostAsJsonAsync($"{BaseUrl}/{created.Id}/resolve", new { });

        resolve.StatusCode.Should().Be(HttpStatusCode.OK);
        var resolved = await ReadAsync<AndonSignalDto>(resolve);
        resolved.Status.Should().Be((short)3);
        resolved.ResolvedAt.Should().NotBeNull();

        var get = await client.GetAsync($"{BaseUrl}/{created.Id}");
        var fetched = await ReadAsync<AndonSignalDto>(get);
        fetched.Status.Should().Be((short)3);
    }

    [Fact]
    public async Task Resolve_WithUnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync($"{BaseUrl}/{Guid.NewGuid()}/resolve", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Resolve_WithResolvedAtEarlierThanRaisedAt_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var created = await RaiseAsync(client, raisedAt: DateTime.UtcNow.AddMinutes(-5));

        var response = await client.PostAsJsonAsync($"{BaseUrl}/{created.Id}/resolve", new
        {
            resolvedAt = DateTime.UtcNow.AddHours(-2)
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_Returns200_AndPersistsChange()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var created = await RaiseAsync(client);

        var update = await client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", new
        {
            id = created.Id,
            category = 2,
            reasonCodeId = (Guid?)null,
            notes = "Updated notes"
        });

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await ReadAsync<AndonSignalDto>(update);
        updated.Category.Should().Be((short)2);
        updated.Notes.Should().Be("Updated notes");
    }

    [Fact]
    public async Task Update_ResolvedSignal_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var created = await RaiseAsync(client);
        await client.PostAsJsonAsync($"{BaseUrl}/{created.Id}/resolve", new { });

        var update = await client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", new
        {
            id = created.Id,
            category = 2,
            reasonCodeId = (Guid?)null,
            notes = "Too late"
        });

        update.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_ActiveSignal_Returns204_AndIsGone()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var created = await RaiseAsync(client);

        var delete = await client.DeleteAsync($"{BaseUrl}/{created.Id}");

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await client.GetAsync($"{BaseUrl}/{created.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ResolvedSignal_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var created = await RaiseAsync(client);
        await client.PostAsJsonAsync($"{BaseUrl}/{created.Id}/resolve", new { });

        var delete = await client.DeleteAsync($"{BaseUrl}/{created.Id}");

        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_WithUnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.DeleteAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Browse_SupportsMachineCategoryAndStatusFilters()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = Guid.NewGuid();
        var created = await RaiseAsync(client, machineId);

        var byMachine = await client.GetAsync($"{BaseUrl}?machineId={machineId}");
        byMachine.StatusCode.Should().Be(HttpStatusCode.OK);
        var machinePage = await ReadAsync<PagedResponseDto<AndonSignalDto>>(byMachine);
        machinePage.Items.Should().ContainSingle(item => item.Id == created.Id);

        var byStatus = await client.GetAsync($"{BaseUrl}?status=1&machineId={machineId}");
        byStatus.StatusCode.Should().Be(HttpStatusCode.OK);
        var statusPage = await ReadAsync<PagedResponseDto<AndonSignalDto>>(byStatus);
        statusPage.Items.Should().ContainSingle(item => item.Id == created.Id);

        var byOtherStatus = await client.GetAsync($"{BaseUrl}?status=3&machineId={machineId}");
        var otherPage = await ReadAsync<PagedResponseDto<AndonSignalDto>>(byOtherStatus);
        otherPage.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_RecordFromAnotherTenant_Returns404()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var created = await RaiseAsync(otherTenantClient);

        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var response = await devClient.GetAsync($"{BaseUrl}/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
