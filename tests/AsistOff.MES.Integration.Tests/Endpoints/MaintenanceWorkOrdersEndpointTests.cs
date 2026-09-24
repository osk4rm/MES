using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>/api/maintenance-work-orders</c>. They exercise
/// the full request pipeline: authentication, tenant resolution, validation
/// behavior, MediatR handlers, EF Core persistence and the global exception
/// handler - against a real PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class MaintenanceWorkOrdersEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/maintenance-work-orders";
    private const string MachinesUrl = "/api/machines";

    // Status values mirror MaintenanceWorkOrderStatus (Open=1, InProgress=2, Done=3, Cancelled=4).

    [Fact]
    public async Task Browse_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.PostAsJsonAsync(BaseUrl, new { code = "WO-X", title = "No auth" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Lifecycle_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();
        var id = Guid.NewGuid();

        (await client.PostAsync($"{BaseUrl}/{id}/start", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync($"{BaseUrl}/{id}/complete", new { resolutionNotes = "x" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsync($"{BaseUrl}/{id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_ReturnsCreated_AndIsRetrievableById()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var code = UniqueCode();

        var createResponse = await client.PostAsJsonAsync(BaseUrl, new
        {
            code,
            title = "Fix spindle",
            description = "Spindle vibrates at high RPM",
            machineId,
            priority = 3
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<MaintenanceWorkOrderDto>(createResponse);
        created.Code.Should().Be(code);
        created.Title.Should().Be("Fix spindle");
        created.MachineId.Should().Be(machineId);
        created.Priority.Should().Be(3);
        created.Status.Should().Be(1);
        created.StartedAt.Should().BeNull();
        created.CompletedAt.Should().BeNull();

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadAsync<MaintenanceWorkOrderDto>(getResponse);
        fetched.Id.Should().Be(created.Id);
        fetched.Code.Should().Be(code);
    }

    [Fact]
    public async Task Create_WithDuplicateCode_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var code = UniqueCode();
        var payload = new { code, title = "Fix spindle", description = (string?)null, machineId, priority = 2 };

        var first = await client.PostAsJsonAsync(BaseUrl, payload);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync(BaseUrl, payload);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_WithUnknownMachine_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            code = UniqueCode(),
            title = "Fix spindle",
            description = (string?)null,
            machineId = Guid.NewGuid(),
            priority = 2
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("", "Fix spindle", 2)]
    [InlineData("WO-1", "", 2)]
    [InlineData("WO-1", "Fix spindle", 99)]
    public async Task Create_WithInvalidPayload_Returns400(string code, string title, int priority)
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            code = code == "WO-1" ? UniqueCode() : code,
            title,
            description = (string?)null,
            machineId,
            priority
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
    public async Task Get_CrossTenantId_Returns404()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var machineId = await CreateMachineAsync(otherTenantClient);
        var createResponse = await otherTenantClient.PostAsJsonAsync(BaseUrl, new
        {
            code = UniqueCode(),
            title = "Other tenant order",
            description = (string?)null,
            machineId,
            priority = 1
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<MaintenanceWorkOrderDto>(createResponse);

        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var getResponse = await devClient.GetAsync($"{BaseUrl}/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var browseResponse = await devClient.GetAsync($"{BaseUrl}?machineId={machineId}");

        browseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<MaintenanceWorkOrderDto>>(browseResponse);
        page.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Browse_FiltersByMachineIdAndStatus()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineA = await CreateMachineAsync(client);
        var machineB = await CreateMachineAsync(client);

        var orderA = await CreateOrderAsync(client, machineA, "Browse A");
        await CreateOrderAsync(client, machineB, "Browse B");
        await client.PostAsync($"{BaseUrl}/{orderA.Id}/start", null);

        var byMachine = await client.GetAsync($"{BaseUrl}?machineId={machineB}");

        byMachine.StatusCode.Should().Be(HttpStatusCode.OK);
        var machinePage = await ReadAsync<PagedResponseDto<MaintenanceWorkOrderDto>>(byMachine);
        machinePage.Items.Should().ContainSingle(i => i.MachineId == machineB);

        var byStatus = await client.GetAsync($"{BaseUrl}?status=2");

        byStatus.StatusCode.Should().Be(HttpStatusCode.OK);
        var statusPage = await ReadAsync<PagedResponseDto<MaintenanceWorkOrderDto>>(byStatus);
        statusPage.Items.Should().Contain(i => i.Id == orderA.Id);
        statusPage.Items.Should().OnlyContain(i => i.Status == 2);
    }

    [Fact]
    public async Task Start_MovesOpenToInProgress_AndSecondStart_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var order = await CreateOrderAsync(client, machineId, "Start me");

        var start = await client.PostAsync($"{BaseUrl}/{order.Id}/start", null);

        start.StatusCode.Should().Be(HttpStatusCode.OK);
        var started = await ReadAsync<MaintenanceWorkOrderDto>(start);
        started.Status.Should().Be(2);
        started.StartedAt.Should().NotBeNull();

        var secondStart = await client.PostAsync($"{BaseUrl}/{order.Id}/start", null);

        secondStart.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Start_WithUnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsync($"{BaseUrl}/{Guid.NewGuid()}/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Complete_MovesToDone_AndRejectsRepeatAndMissingNotes()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var order = await CreateOrderAsync(client, machineId, "Complete me");

        var withoutNotes = await client.PostAsJsonAsync(
            $"{BaseUrl}/{order.Id}/complete", new { resolutionNotes = "" });

        withoutNotes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var complete = await client.PostAsJsonAsync(
            $"{BaseUrl}/{order.Id}/complete", new { resolutionNotes = "Replaced bearing" });

        complete.StatusCode.Should().Be(HttpStatusCode.OK);
        var done = await ReadAsync<MaintenanceWorkOrderDto>(complete);
        done.Status.Should().Be(3);
        done.ResolutionNotes.Should().Be("Replaced bearing");
        done.CompletedAt.Should().NotBeNull();

        var repeat = await client.PostAsJsonAsync(
            $"{BaseUrl}/{order.Id}/complete", new { resolutionNotes = "Again" });

        repeat.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cancel_MovesToCancelled_AndRejectsRepeatAfterDone()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var cancellable = await CreateOrderAsync(client, machineId, "Cancel me");
        var completable = await CreateOrderAsync(client, machineId, "Finish then cancel");

        var cancel = await client.PostAsync($"{BaseUrl}/{cancellable.Id}/cancel", null);

        cancel.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelled = await ReadAsync<MaintenanceWorkOrderDto>(cancel);
        cancelled.Status.Should().Be(4);

        var repeatCancel = await client.PostAsync($"{BaseUrl}/{cancellable.Id}/cancel", null);

        repeatCancel.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await client.PostAsJsonAsync($"{BaseUrl}/{completable.Id}/complete", new { resolutionNotes = "Done" });
        var cancelDone = await client.PostAsync($"{BaseUrl}/{completable.Id}/cancel", null);

        cancelDone.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<Guid> CreateMachineAsync(HttpClient client)
    {
        var code = $"MC-{Guid.NewGuid():N}"[..12];
        var response = await client.PostAsJsonAsync(MachinesUrl, new
        {
            code,
            name = $"Machine {code}",
            description = (string?)null,
            isActive = true,
            departmentId = (Guid?)null,
            syncId = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var machine = await ReadAsync<MachineDto>(response);
        return machine.Id;
    }

    private async Task<MaintenanceWorkOrderDto> CreateOrderAsync(HttpClient client, Guid machineId, string title)
    {
        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            code = UniqueCode(),
            title,
            description = (string?)null,
            machineId,
            priority = 2
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<MaintenanceWorkOrderDto>(response);
    }

    private static string UniqueCode() => $"WO-{Guid.NewGuid():N}"[..12];
}
