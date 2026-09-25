using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>/api/reliability/trend</c>.
/// They exercise the full request pipeline: authentication, tenant
/// resolution, the read-time per-bucket MTBF/MTTR computation over downtime
/// and maintenance rows and the global exception handler - against a real
/// PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ReliabilityTrendEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/reliability/trend";
    private const string SnapshotUrl = "/api/reliability/snapshot";

    [Fact]
    public async Task Trend_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={Guid.NewGuid()}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}&bucket=Day");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Trend_TwoWeekWeekBuckets_ReturnsOneEntryPerBucketMatchingSnapshot()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var fromUtc = DateTime.UtcNow.AddDays(-14);
        var machine = await CreateMachineAsync(client);
        var reasonId = await CreateReasonAsync(client);

        var downtimeStart = DateTime.UtcNow.AddHours(-2);
        await StartAndCloseDowntimeAsync(client, machine.Id, reasonId, downtimeStart, downtimeStart.AddMinutes(60));

        var order = await CreateWorkOrderAsync(client, machine.Id);
        var completed = await CompleteWorkOrderAsync(client, order.Id);

        var toUtc = completed.CompletedAt!.Value.AddMinutes(1);
        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}&bucket=Week");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var trend = await ReadAsync<ReliabilityTrendDto>(response);

        trend.MachineId.Should().Be(machine.Id);
        trend.FromUtc.Should().Be(fromUtc.ToUniversalTime());
        trend.ToUtc.Should().Be(toUtc.ToUniversalTime());
        trend.Bucket.Should().Be("Week");
        trend.Buckets.Should().NotBeEmpty();
        // A 14-day window always crosses a Monday 00:00 UTC boundary.
        trend.Buckets.Should().HaveCountGreaterThan(1);
        trend.Buckets.Select(b => b.FromUtc).Should().BeInAscendingOrder();
        trend.Buckets.First().FromUtc.Should().Be(fromUtc.ToUniversalTime());
        trend.Buckets.Last().ToUtc.Should().Be(toUtc.ToUniversalTime());

        var ordered = trend.Buckets.OrderBy(b => b.FromUtc).ToList();
        for (var i = 0; i < ordered.Count - 1; i++)
            ordered[i].ToUtc.Should().Be(ordered[i + 1].FromUtc);

        // Every bucket entry equals the snapshot for that sub-window.
        foreach (var bucket in ordered)
        {
            var bucketSnapshotResponse = await client.GetAsync(
                $"{SnapshotUrl}?machineId={machine.Id}&fromUtc={Qs(bucket.FromUtc)}&toUtc={Qs(bucket.ToUtc)}");
            bucketSnapshotResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var bucketSnapshot = await ReadAsync<ReliabilitySnapshotDto>(bucketSnapshotResponse);
            bucket.Should().Be(bucketSnapshot);
        }
    }

    [Fact]
    public async Task Trend_ZeroFailures_ReturnsNullMtbfMttr()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var fromUtc = DateTime.UtcNow.AddDays(-2);
        var toUtc = DateTime.UtcNow.AddMinutes(1);
        var machine = await CreateMachineAsync(client);

        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}&bucket=Day");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var trend = await ReadAsync<ReliabilityTrendDto>(response);

        trend.Buckets.Should().NotBeEmpty();
        trend.Buckets.Should().OnlyContain(b =>
            b.FailureCount == 0
            && b.MtbfMinutes == null
            && b.MttrMinutes == null);
    }

    [Fact]
    public async Task Trend_OpenDowntime_DoesNotCountFailure()
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
            $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}&bucket=Day");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var trend = await ReadAsync<ReliabilityTrendDto>(response);

        trend.Buckets.Should().NotBeEmpty();
        trend.Buckets.Select(b => b.FailureCount).Sum().Should().Be(0);
        trend.Buckets.Select(b => b.TotalDowntimeMinutes).Sum().Should().Be(0);
        trend.Buckets.Should().OnlyContain(b =>
            b.MtbfMinutes == null && b.MttrMinutes == null);
    }

    [Fact]
    public async Task Trend_OpenWorkOrder_DoesNotCountRepair()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var fromUtc = DateTime.UtcNow.AddHours(-8);
        var machine = await CreateMachineAsync(client);
        await CreateWorkOrderAsync(client, machine.Id);

        var toUtc = DateTime.UtcNow.AddMinutes(1);
        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}&bucket=Day");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var trend = await ReadAsync<ReliabilityTrendDto>(response);

        trend.Buckets.Should().NotBeEmpty();
        trend.Buckets.Select(b => b.RepairCount).Sum().Should().Be(0);
        trend.Buckets.Should().OnlyContain(b => b.AvgRepairMinutes == null);
    }

    [Fact]
    public async Task Trend_UnknownMachine_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={Guid.NewGuid()}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}&bucket=Day");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Trend_CrossTenantMachine_Returns404()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var foreignMachine = await CreateMachineAsync(otherTenantClient);

        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={foreignMachine.Id}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}&bucket=Day");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Trend_MissingMachineId_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={Guid.Empty}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}&bucket=Day");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Trend_ReversedWindow_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(DateTime.UtcNow)}&toUtc={Qs(DateTime.UtcNow.AddHours(-8))}&bucket=Day");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Trend_WindowOver93Days_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(DateTime.UtcNow.AddDays(-100))}&toUtc={Qs(DateTime.UtcNow)}&bucket=Day");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("Month")]
    [InlineData("daily")]
    [InlineData("")]
    public async Task Trend_UnknownBucket_Returns400(string bucket)
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}&bucket={bucket}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
