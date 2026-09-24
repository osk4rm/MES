using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>/api/downtime-events</c>. They exercise
/// the full request pipeline: authentication, tenant resolution, MediatR handler,
/// EF Core persistence and the global exception handler - against a real
/// PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class DowntimeEventsEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/downtime-events";

    [Fact]
    public async Task Browse_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Start_ReturnsCreated_AndIsRetrievableById()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var (machineId, reasonCodeId) = await CreateMachineAndReasonAsync(client);
        var startedAt = DateTime.UtcNow.AddHours(-1);

        var startResponse = await client.PostAsJsonAsync(BaseUrl, new
        {
            machineId,
            reasonCodeId,
            startedAt,
            notes = "belt jam",
            reportedByOperatorId = (Guid?)null
        });

        startResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<DowntimeEventDto>(startResponse);
        created.MachineId.Should().Be(machineId);
        created.EndedAt.Should().BeNull();
        created.Status.Should().Be((short)1); // Open
        created.DurationMinutes.Should().BeNull();
        created.ProductionOrderId.Should().BeNull();

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadAsync<DowntimeEventDto>(getResponse);
        fetched.Id.Should().Be(created.Id);
        fetched.Notes.Should().Be("belt jam");
    }

    [Fact]
    public async Task Start_SecondOpenEventOnSameMachine_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var (machineId, reasonCodeId) = await CreateMachineAndReasonAsync(client);
        var payload = new
        {
            machineId,
            reasonCodeId,
            startedAt = DateTime.UtcNow.AddHours(-2),
            notes = (string?)null,
            reportedByOperatorId = (Guid?)null
        };

        var first = await client.PostAsJsonAsync(BaseUrl, payload);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync(BaseUrl, payload);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Start_WithFutureStartedAt_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var (machineId, reasonCodeId) = await CreateMachineAndReasonAsync(client);

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            machineId,
            reasonCodeId,
            startedAt = DateTime.UtcNow.AddHours(2),
            notes = (string?)null,
            reportedByOperatorId = (Guid?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Close_ReturnsOk_WithComputedDuration()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var (machineId, reasonCodeId) = await CreateMachineAndReasonAsync(client);
        var startedAt = DateTime.UtcNow.AddHours(-3);
        var created = await StartAsync(client, machineId, reasonCodeId, startedAt);
        var endedAt = startedAt.AddMinutes(90);

        var closeResponse = await client.PostAsJsonAsync($"{BaseUrl}/{created.Id}/close", new { endedAt });

        closeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var closed = await ReadAsync<DowntimeEventDto>(closeResponse);
        closed.EndedAt.Should().BeCloseTo(endedAt, TimeSpan.FromSeconds(1));
        closed.Status.Should().Be((short)2); // Closed
        closed.DurationMinutes.Should().BeApproximately(90, 0.5);
    }

    [Fact]
    public async Task Close_AlreadyClosed_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var (machineId, reasonCodeId) = await CreateMachineAndReasonAsync(client);
        var created = await StartAsync(client, machineId, reasonCodeId, DateTime.UtcNow.AddHours(-3));
        var first = await client.PostAsJsonAsync($"{BaseUrl}/{created.Id}/close", new { endedAt = DateTime.UtcNow.AddHours(-1) });
        first.EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync($"{BaseUrl}/{created.Id}/close", new { endedAt = DateTime.UtcNow });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Close_WithEndedAtBeforeStartedAt_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var (machineId, reasonCodeId) = await CreateMachineAndReasonAsync(client);
        var startedAt = DateTime.UtcNow.AddHours(-1);
        var created = await StartAsync(client, machineId, reasonCodeId, startedAt);

        var response = await client.PostAsJsonAsync($"{BaseUrl}/{created.Id}/close", new { endedAt = startedAt.AddMinutes(-5) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_OpenEvent_Returns204_AndPersistsChanges()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var (machineId, reasonCodeId) = await CreateMachineAndReasonAsync(client);
        var created = await StartAsync(client, machineId, reasonCodeId, DateTime.UtcNow.AddHours(-1));
        var newReasonCodeId = Guid.NewGuid();

        var updateResponse = await client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", new
        {
            id = created.Id,
            reasonCodeId = newReasonCodeId,
            notes = "updated notes"
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var fetched = await ReadAsync<DowntimeEventDto>(await client.GetAsync($"{BaseUrl}/{created.Id}"));
        fetched.ReasonCodeId.Should().Be(newReasonCodeId);
        fetched.Notes.Should().Be("updated notes");
    }

    [Fact]
    public async Task Update_ClosedEvent_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var (machineId, reasonCodeId) = await CreateMachineAndReasonAsync(client);
        var created = await StartAsync(client, machineId, reasonCodeId, DateTime.UtcNow.AddHours(-3));
        var close = await client.PostAsJsonAsync($"{BaseUrl}/{created.Id}/close", new { endedAt = DateTime.UtcNow.AddHours(-1) });
        close.EnsureSuccessStatusCode();

        var updateResponse = await client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", new
        {
            id = created.Id,
            reasonCodeId,
            notes = "too late"
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_OpenEvent_Returns204_AndIsGone()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var (machineId, reasonCodeId) = await CreateMachineAndReasonAsync(client);
        var created = await StartAsync(client, machineId, reasonCodeId, DateTime.UtcNow.AddHours(-1));

        var deleteResponse = await client.DeleteAsync($"{BaseUrl}/{created.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ClosedEvent_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var (machineId, reasonCodeId) = await CreateMachineAndReasonAsync(client);
        var created = await StartAsync(client, machineId, reasonCodeId, DateTime.UtcNow.AddHours(-3));
        var close = await client.PostAsJsonAsync($"{BaseUrl}/{created.Id}/close", new { endedAt = DateTime.UtcNow.AddHours(-1) });
        close.EnsureSuccessStatusCode();

        var deleteResponse = await client.DeleteAsync($"{BaseUrl}/{created.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Get_WithUnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Browse_FiltersByMachineAndStatus()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var (machineA, reasonCodeId) = await CreateMachineAndReasonAsync(client);
        var (machineB, _) = await CreateMachineAndReasonAsync(client);
        var openOnA = await StartAsync(client, machineA, reasonCodeId, DateTime.UtcNow.AddHours(-4));
        var openOnB = await StartAsync(client, machineB, reasonCodeId, DateTime.UtcNow.AddHours(-4));
        var close = await client.PostAsJsonAsync($"{BaseUrl}/{openOnB.Id}/close", new { endedAt = DateTime.UtcNow.AddHours(-1) });
        close.EnsureSuccessStatusCode();

        var byMachine = await client.GetAsync($"{BaseUrl}?machineId={machineA}");
        byMachine.StatusCode.Should().Be(HttpStatusCode.OK);
        var machinePage = await ReadAsync<PagedResponseDto<DowntimeEventDto>>(byMachine);
        machinePage.Items.Should().Contain(item => item.Id == openOnA.Id);
        machinePage.Items.Should().NotContain(item => item.Id == openOnB.Id);

        var openOnly = await client.GetAsync($"{BaseUrl}?status=1");
        openOnly.StatusCode.Should().Be(HttpStatusCode.OK);
        var openPage = await ReadAsync<PagedResponseDto<DowntimeEventDto>>(openOnly);
        openPage.Items.Should().Contain(item => item.Id == openOnA.Id);
        openPage.Items.Should().NotContain(item => item.Id == openOnB.Id);
    }

    [Fact]
    public async Task Get_ClosedEvent_ReturnsComputedDuration()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var (machineId, reasonCodeId) = await CreateMachineAndReasonAsync(client);
        var startedAt = DateTime.UtcNow.AddHours(-3);
        var created = await StartAsync(client, machineId, reasonCodeId, startedAt);
        var endedAt = startedAt.AddMinutes(90);
        var close = await client.PostAsJsonAsync($"{BaseUrl}/{created.Id}/close", new { endedAt });
        close.EnsureSuccessStatusCode();

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadAsync<DowntimeEventDto>(getResponse);
        fetched.EndedAt.Should().BeCloseTo(endedAt, TimeSpan.FromSeconds(1));
        fetched.Status.Should().Be((short)2); // Closed
        fetched.DurationMinutes.Should().NotBeNull();
        fetched.DurationMinutes!.Value.Should().BeApproximately(90, 0.5);
    }

    [Fact]
    public async Task Browse_FiltersByReasonCode()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var (machineA, reasonA) = await CreateMachineAndReasonAsync(client);
        var (machineB, reasonB) = await CreateMachineAndReasonAsync(client);
        var eventA = await StartAsync(client, machineA, reasonA, DateTime.UtcNow.AddHours(-2));
        var eventB = await StartAsync(client, machineB, reasonB, DateTime.UtcNow.AddHours(-2));

        var response = await client.GetAsync($"{BaseUrl}?reasonCodeId={reasonA}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<DowntimeEventDto>>(response);
        page.Items.Should().Contain(item => item.Id == eventA.Id);
        page.Items.Should().NotContain(item => item.Id == eventB.Id);
    }

    [Fact]
    public async Task Browse_FiltersByClosedStatus()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var (machineA, reasonCodeId) = await CreateMachineAndReasonAsync(client);
        var (machineB, _) = await CreateMachineAndReasonAsync(client);
        var openEvent = await StartAsync(client, machineA, reasonCodeId, DateTime.UtcNow.AddHours(-4));
        var toClose = await StartAsync(client, machineB, reasonCodeId, DateTime.UtcNow.AddHours(-4));
        var close = await client.PostAsJsonAsync($"{BaseUrl}/{toClose.Id}/close", new { endedAt = DateTime.UtcNow.AddHours(-1) });
        close.EnsureSuccessStatusCode();

        var response = await client.GetAsync($"{BaseUrl}?status=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<DowntimeEventDto>>(response);
        page.Items.Should().Contain(item => item.Id == toClose.Id);
        page.Items.Should().NotContain(item => item.Id == openEvent.Id);
    }

    [Fact]
    public async Task Browse_FiltersByStartedAtRange()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var (machineOld, reasonOld) = await CreateMachineAndReasonAsync(client);
        var (machineRecent, reasonRecent) = await CreateMachineAndReasonAsync(client);
        var oldEvent = await StartAsync(client, machineOld, reasonOld, DateTime.UtcNow.AddDays(-30));
        var recentEvent = await StartAsync(client, machineRecent, reasonRecent, DateTime.UtcNow.AddHours(-1));
        var from = Uri.EscapeDataString(DateTime.UtcNow.AddDays(-7).ToString("O"));
        var to = Uri.EscapeDataString(DateTime.UtcNow.ToString("O"));

        var response = await client.GetAsync($"{BaseUrl}?startedFrom={from}&startedTo={to}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<DowntimeEventDto>>(response);
        page.Items.Should().Contain(item => item.Id == recentEvent.Id);
        page.Items.Should().NotContain(item => item.Id == oldEvent.Id);
    }

    [Fact]
    public async Task Browse_SupportsPaging()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var (machineA, reasonA) = await CreateMachineAndReasonAsync(client);
        var (machineB, reasonB) = await CreateMachineAndReasonAsync(client);
        var (machineC, reasonC) = await CreateMachineAndReasonAsync(client);
        await StartAsync(client, machineA, reasonA, DateTime.UtcNow.AddHours(-3));
        await StartAsync(client, machineB, reasonB, DateTime.UtcNow.AddHours(-2));
        await StartAsync(client, machineC, reasonC, DateTime.UtcNow.AddHours(-1));

        var firstPage = await client.GetAsync($"{BaseUrl}?pageNumber=1&pageSize=2");

        firstPage.StatusCode.Should().Be(HttpStatusCode.OK);
        var first = await ReadAsync<PagedResponseDto<DowntimeEventDto>>(firstPage);
        first.TotalCount.Should().Be(3);
        first.TotalPages.Should().Be(2);
        first.Items.Should().HaveCount(2);

        var secondPage = await client.GetAsync($"{BaseUrl}?pageNumber=2&pageSize=2");

        secondPage.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await ReadAsync<PagedResponseDto<DowntimeEventDto>>(secondPage);
        second.TotalCount.Should().Be(3);
        second.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Browse_DoesNotLeakEventsFromAnotherTenant()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var (machineId, reasonCodeId) = await CreateMachineAndReasonAsync(otherTenantClient);
        var created = await StartAsync(otherTenantClient, machineId, reasonCodeId, DateTime.UtcNow.AddHours(-1));

        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var response = await devClient.GetAsync($"{BaseUrl}?machineId={machineId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<DowntimeEventDto>>(response);
        page.Items.Should().NotContain(item => item.Id == created.Id);
    }

    private static async Task<(Guid MachineId, Guid ReasonCodeId)> CreateMachineAndReasonAsync(HttpClient client)
    {
        var machineCode = $"DT-M-{Guid.NewGuid():N}"[..12];
        var machineResponse = await client.PostAsJsonAsync("/api/machines", new
        {
            code = machineCode,
            name = machineCode,
            description = (string?)null,
            departmentId = (Guid?)null,
            isActive = true
        });
        machineResponse.EnsureSuccessStatusCode();
        var machine = await ReadAsync<MachineDto>(machineResponse);

        var reasonCode = $"DT-R-{Guid.NewGuid():N}"[..12];
        var reasonResponse = await client.PostAsJsonAsync("/api/reason-codes", new
        {
            code = reasonCode,
            name = reasonCode,
            description = (string?)null,
            category = 1,
            isActive = true,
            sortIndex = 0
        });
        reasonResponse.EnsureSuccessStatusCode();
        var reason = await ReadAsync<ReasonCodeDto>(reasonResponse);

        return (machine.Id, reason.Id);
    }

    private static async Task<DowntimeEventDto> StartAsync(HttpClient client, Guid machineId, Guid reasonCodeId, DateTime startedAt)
    {
        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            machineId,
            reasonCodeId,
            startedAt,
            notes = (string?)null,
            reportedByOperatorId = (Guid?)null
        });
        response.EnsureSuccessStatusCode();
        return await ReadAsync<DowntimeEventDto>(response);
    }
}
