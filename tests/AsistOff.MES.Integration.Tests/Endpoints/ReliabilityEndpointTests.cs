using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>/api/reliability/snapshot</c>.
/// They exercise the full request pipeline: authentication, tenant
/// resolution, the read-time MTBF/MTTR computation over downtime and
/// maintenance rows and the global exception handler - against a real
/// PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ReliabilityEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/reliability/snapshot";

    [Fact]
    public async Task Snapshot_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={Guid.NewGuid()}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Snapshot_HappyPath_ReturnsHandComputedValues()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var fromUtc = DateTime.UtcNow.AddHours(-8);
        var machine = await CreateMachineAsync(client);
        var reasonId = await CreateReasonAsync(client);

        var downtimeStart = DateTime.UtcNow.AddHours(-2);
        await StartAndCloseDowntimeAsync(client, machine.Id, reasonId, downtimeStart, downtimeStart.AddMinutes(60));

        var order = await CreateWorkOrderAsync(client, machine.Id);
        var completed = await CompleteWorkOrderAsync(client, order.Id);

        var toUtc = completed.CompletedAt!.Value.AddMinutes(1);
        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var snapshot = await ReadAsync<ReliabilitySnapshotDto>(response);

        var expectedWindow = (toUtc.ToUniversalTime() - fromUtc.ToUniversalTime()).TotalMinutes;
        var expectedUptime = expectedWindow - 60;

        snapshot.MachineId.Should().Be(machine.Id);
        snapshot.FromUtc.Should().Be(fromUtc.ToUniversalTime());
        snapshot.ToUtc.Should().Be(toUtc.ToUniversalTime());
        snapshot.FailureCount.Should().Be(1);
        snapshot.TotalDowntimeMinutes.Should().BeApproximately(60, 0.01);
        snapshot.WindowMinutes.Should().BeApproximately(expectedWindow, 0.05);
        snapshot.UptimeMinutes.Should().BeApproximately(expectedUptime, 0.05);
        snapshot.MtbfMinutes.Should().NotBeNull();
        snapshot.MtbfMinutes!.Value.Should().BeApproximately(expectedUptime, 0.05);
        snapshot.MttrMinutes.Should().NotBeNull();
        snapshot.MttrMinutes!.Value.Should().BeApproximately(60, 0.01);
        snapshot.RepairCount.Should().Be(1);
        snapshot.AvgRepairMinutes.Should().NotBeNull();
        snapshot.AvgRepairMinutes!.Value.Should().BeGreaterThanOrEqualTo(0);

        var expectedRepair = (completed.CompletedAt!.Value
            - (completed.StartedAt ?? completed.ReportedAt)).TotalMinutes;
        snapshot.AvgRepairMinutes!.Value.Should().BeApproximately(
            Math.Round(expectedRepair, 2), 0.05);
    }

    [Fact]
    public async Task Snapshot_ZeroFailures_ReturnsNullMtbfMttr()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var fromUtc = DateTime.UtcNow.AddHours(-8);
        var toUtc = DateTime.UtcNow.AddMinutes(1);
        var machine = await CreateMachineAsync(client);

        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var snapshot = await ReadAsync<ReliabilitySnapshotDto>(response);

        snapshot.FailureCount.Should().Be(0);
        snapshot.TotalDowntimeMinutes.Should().Be(0);
        snapshot.MtbfMinutes.Should().BeNull();
        snapshot.MttrMinutes.Should().BeNull();
        snapshot.RepairCount.Should().Be(0);
        snapshot.AvgRepairMinutes.Should().BeNull();
    }

    [Fact]
    public async Task Snapshot_OpenDowntime_DoesNotCountFailure()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var fromUtc = DateTime.UtcNow.AddHours(-2);
        var machine = await CreateMachineAsync(client);
        var reasonId = await CreateReasonAsync(client);

        var startResponse = await client.PostAsJsonAsync("/api/downtime-events", new
        {
            machineId = machine.Id,
            reasonCodeId = reasonId,
            startedAt = DateTime.UtcNow.AddHours(-1),
            notes = (string?)null,
            reportedByOperatorId = (Guid?)null
        });
        startResponse.EnsureSuccessStatusCode();

        var toUtc = DateTime.UtcNow.AddMinutes(1);
        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var snapshot = await ReadAsync<ReliabilitySnapshotDto>(response);

        snapshot.FailureCount.Should().Be(0);
        snapshot.TotalDowntimeMinutes.Should().Be(0);
        snapshot.MtbfMinutes.Should().BeNull();
        snapshot.MttrMinutes.Should().BeNull();
    }

    [Fact]
    public async Task Snapshot_OpenWorkOrder_DoesNotCountRepair()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var fromUtc = DateTime.UtcNow.AddHours(-8);
        var machine = await CreateMachineAsync(client);
        await CreateWorkOrderAsync(client, machine.Id);

        var toUtc = DateTime.UtcNow.AddMinutes(1);
        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var snapshot = await ReadAsync<ReliabilitySnapshotDto>(response);

        snapshot.RepairCount.Should().Be(0);
        snapshot.AvgRepairMinutes.Should().BeNull();
    }

    [Fact]
    public async Task Snapshot_ReversedWindow_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(DateTime.UtcNow)}&toUtc={Qs(DateTime.UtcNow.AddHours(-8))}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Snapshot_WindowOver93Days_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(DateTime.UtcNow.AddDays(-100))}&toUtc={Qs(DateTime.UtcNow)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Snapshot_UnknownMachine_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={Guid.NewGuid()}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Snapshot_CrossTenantMachine_Returns404()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var foreignMachine = await CreateMachineAsync(otherTenantClient);

        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={foreignMachine.Id}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static string Qs(DateTime value) => Uri.EscapeDataString(value.ToString("O"));

    private static async Task<MachineDto> CreateMachineAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/machines", new
        {
            code = $"REL-{Guid.NewGuid():N}"[..12],
            name = "Reliability Work Center",
            description = (string?)null,
            departmentId = (Guid?)null,
            isActive = true
        });

        response.EnsureSuccessStatusCode();
        return await ReadAsync<MachineDto>(response);
    }

    private static async Task<Guid> CreateReasonAsync(HttpClient client)
    {
        var code = $"REL-{Guid.NewGuid():N}"[..12];
        var response = await client.PostAsJsonAsync("/api/reason-codes", new
        {
            code,
            name = code,
            description = (string?)null,
            category = (short)1,
            isActive = true,
            sortIndex = 0
        });

        response.EnsureSuccessStatusCode();
        return (await ReadAsync<ReasonCodeDto>(response)).Id;
    }

    private static async Task StartAndCloseDowntimeAsync(
        HttpClient client, Guid machineId, Guid reasonId, DateTime startedAt, DateTime endedAt)
    {
        var startResponse = await client.PostAsJsonAsync("/api/downtime-events", new
        {
            machineId,
            reasonCodeId = reasonId,
            startedAt,
            notes = (string?)null,
            reportedByOperatorId = (Guid?)null
        });
        startResponse.EnsureSuccessStatusCode();
        var started = await ReadAsync<DowntimeEventDto>(startResponse);

        var closeResponse = await client.PostAsJsonAsync(
            $"/api/downtime-events/{started.Id}/close", new { endedAt });
        closeResponse.EnsureSuccessStatusCode();
    }

    private static async Task<MaintenanceWorkOrderDto> CreateWorkOrderAsync(HttpClient client, Guid machineId)
    {
        var response = await client.PostAsJsonAsync("/api/maintenance-work-orders", new
        {
            code = $"WO-{Guid.NewGuid():N}"[..12],
            title = "Reliability repair",
            description = (string?)null,
            machineId,
            priority = 2
        });

        response.EnsureSuccessStatusCode();
        return await ReadAsync<MaintenanceWorkOrderDto>(response);
    }

    private static async Task<MaintenanceWorkOrderDto> CompleteWorkOrderAsync(HttpClient client, Guid orderId)
    {
        var startResponse = await client.PostAsync($"/api/maintenance-work-orders/{orderId}/start", null);
        startResponse.EnsureSuccessStatusCode();

        var completeResponse = await client.PostAsJsonAsync(
            $"/api/maintenance-work-orders/{orderId}/complete", new { resolutionNotes = "Fixed" });
        completeResponse.EnsureSuccessStatusCode();
        return await ReadAsync<MaintenanceWorkOrderDto>(completeResponse);
    }
}
