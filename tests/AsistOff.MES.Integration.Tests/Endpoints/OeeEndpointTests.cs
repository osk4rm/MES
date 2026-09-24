using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>/api/oee/snapshot</c>. They exercise
/// the full request pipeline: authentication, tenant resolution, the read-time
/// OEE computation over calendar/downtime/confirmation rows and the global
/// exception handler - against a real PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class OeeEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/oee/snapshot";
    private const string TrendUrl = "/api/oee/trend";
    private const string LossesUrl = "/api/oee/losses";

    [Fact]
    public async Task Snapshot_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={Guid.NewGuid()}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}&idealCycleTimeSeconds=60");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Snapshot_HappyPath_ReturnsHandComputedFactors()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var fromUtc = DateTime.UtcNow.AddHours(-8);
        var machine = await CreateMachineAsync(client);
        await PutFullDayCalendarAsync(client, machine.Id, fromUtc, DateTime.UtcNow);
        var reasonId = await CreateReasonAsync(client);
        var order = await CreateReleasedOrderAsync(client);

        var downtimeStart = DateTime.UtcNow.AddHours(-2);
        await StartAndCloseDowntimeAsync(client, machine.Id, reasonId, downtimeStart, downtimeStart.AddMinutes(60));

        var confirmResponse = await client.PostAsJsonAsync("/api/production-confirmations", new
        {
            productionOrderId = order.Id,
            machineId = machine.Id,
            reportedByOperatorId = (Guid?)null,
            reportedAt = DateTime.UtcNow,
            goodQuantity = 90m,
            scrapQuantity = 10m,
            notes = (string?)null
        });
        confirmResponse.EnsureSuccessStatusCode();
        var confirmation = await ReadAsync<ProductionConfirmationDto>(confirmResponse);

        var toUtc = confirmation.ReportedAt.AddMinutes(1);
        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}&idealCycleTimeSeconds=60");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var snapshot = await ReadAsync<OeeSnapshotDto>(response);

        var planned = (toUtc - fromUtc).TotalMinutes;
        var run = planned - 60;
        var availability = Math.Round(run / planned, 4);
        var performance = Math.Round(100.0 / run, 4);
        var oee = Math.Round(availability * performance * 0.9, 4);

        snapshot.MachineId.Should().Be(machine.Id);
        snapshot.FromUtc.Should().Be(fromUtc.ToUniversalTime());
        snapshot.ToUtc.Should().Be(toUtc.ToUniversalTime());
        snapshot.IdealCycleTimeSeconds.Should().Be(60m);
        snapshot.PlannedProductionTimeMinutes.Should().BeApproximately(planned, 0.001);
        snapshot.DowntimeMinutes.Should().BeApproximately(60, 0.001);
        snapshot.RunTimeMinutes.Should().BeApproximately(run, 0.001);
        snapshot.TotalCount.Should().Be(100m);
        snapshot.GoodCount.Should().Be(90m);
        snapshot.ScrapCount.Should().Be(10m);
        snapshot.Availability.Should().BeApproximately(availability, 0.0001);
        snapshot.Performance.Should().BeApproximately(performance, 0.0001);
        snapshot.Quality.Should().BeApproximately(0.9, 0.0001);
        snapshot.Oee.Should().BeApproximately(oee, 0.0001);
        snapshot.AvailabilityComputed.Should().BeTrue();
        snapshot.PerformanceComputed.Should().BeTrue();
        snapshot.QualityComputed.Should().BeTrue();
    }

    [Fact]
    public async Task Snapshot_UnknownMachine_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={Guid.NewGuid()}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}&idealCycleTimeSeconds=60");

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
            $"{BaseUrl}?machineId={foreignMachine.Id}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}&idealCycleTimeSeconds=60");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Snapshot_ReversedWindow_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(DateTime.UtcNow)}&toUtc={Qs(DateTime.UtcNow.AddHours(-8))}&idealCycleTimeSeconds=60");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    public async Task Snapshot_NonPositiveIdealCycleTime_Returns400(string ideal)
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}&idealCycleTimeSeconds={ideal}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Snapshot_WindowOver93Days_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);
        var from = DateTime.UtcNow.AddDays(-100);

        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(from)}&toUtc={Qs(DateTime.UtcNow)}&idealCycleTimeSeconds=60");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Snapshot_NoPlannedTime_ReturnsNullFactors()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}&idealCycleTimeSeconds=60");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var snapshot = await ReadAsync<OeeSnapshotDto>(response);
        snapshot.Availability.Should().BeNull();
        snapshot.Performance.Should().BeNull();
        snapshot.Quality.Should().BeNull();
        snapshot.Oee.Should().BeNull();
        snapshot.AvailabilityComputed.Should().BeFalse();
        snapshot.PerformanceComputed.Should().BeFalse();
        snapshot.QualityComputed.Should().BeFalse();
        snapshot.PlannedProductionTimeMinutes.Should().Be(0);
    }

    [Fact]
    public async Task Snapshot_OpenDowntime_DoesNotReduceRunTime()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var fromUtc = DateTime.UtcNow.AddHours(-2);
        var machine = await CreateMachineAsync(client);
        await PutFullDayCalendarAsync(client, machine.Id, fromUtc, DateTime.UtcNow);
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
            $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}&idealCycleTimeSeconds=60");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var snapshot = await ReadAsync<OeeSnapshotDto>(response);
        snapshot.DowntimeMinutes.Should().Be(0);
        snapshot.RunTimeMinutes.Should().BeApproximately(snapshot.PlannedProductionTimeMinutes, 0.001);
        snapshot.Availability.Should().BeApproximately(1.0, 0.0001);
    }

    [Fact]
    public async Task Snapshot_ScrapEvent_LeavesSnapshotUnchanged()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var fromUtc = DateTime.UtcNow.AddHours(-8);
        var machine = await CreateMachineAsync(client);
        await PutFullDayCalendarAsync(client, machine.Id, fromUtc, DateTime.UtcNow);
        var reasonId = await CreateReasonAsync(client);
        var order = await CreateReleasedOrderAsync(client);

        var confirmResponse = await client.PostAsJsonAsync("/api/production-confirmations", new
        {
            productionOrderId = order.Id,
            machineId = machine.Id,
            reportedByOperatorId = (Guid?)null,
            reportedAt = DateTime.UtcNow,
            goodQuantity = 90m,
            scrapQuantity = 10m,
            notes = (string?)null
        });
        confirmResponse.EnsureSuccessStatusCode();
        var confirmation = await ReadAsync<ProductionConfirmationDto>(confirmResponse);
        var toUtc = confirmation.ReportedAt.AddMinutes(1);
        var url = $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}&idealCycleTimeSeconds=60";

        var before = await ReadAsync<OeeSnapshotDto>(await client.GetAsync(url));

        var scrapResponse = await client.PostAsJsonAsync("/api/scrap-events", new
        {
            machineId = machine.Id,
            reasonCodeId = reasonId,
            quantity = 25m,
            reportedAt = DateTime.UtcNow,
            notes = (string?)null,
            reportedByOperatorId = (Guid?)null,
            productionOrderId = (Guid?)null
        });
        scrapResponse.EnsureSuccessStatusCode();

        var afterResponse = await client.GetAsync(url);
        afterResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await ReadAsync<OeeSnapshotDto>(afterResponse);

        after.Should().Be(before);
        after.TotalCount.Should().Be(100m);
        after.GoodCount.Should().Be(90m);
        after.Quality.Should().BeApproximately(0.9, 0.0001);
    }

    private static string Qs(DateTime value) => Uri.EscapeDataString(value.ToString("O"));

    [Fact]
    public async Task Trend_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(
            $"{TrendUrl}?machineId={Guid.NewGuid()}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}&idealCycleTimeSeconds=60&bucket=Day");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Losses_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(
            $"{LossesUrl}?machineId={Guid.NewGuid()}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Trend_HappyPath_EntriesMatchPerBucketSnapshots()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var fromUtc = DateTime.UtcNow.AddHours(-30);
        var machine = await CreateMachineAsync(client);
        await PutFullDayCalendarAsync(client, machine.Id, fromUtc, DateTime.UtcNow);
        var reasonId = await CreateReasonAsync(client);
        var order = await CreateReleasedOrderAsync(client);

        var downtimeStart = DateTime.UtcNow.AddHours(-20);
        await StartAndCloseDowntimeAsync(client, machine.Id, reasonId, downtimeStart, downtimeStart.AddMinutes(45));

        var confirmResponse = await client.PostAsJsonAsync("/api/production-confirmations", new
        {
            productionOrderId = order.Id,
            machineId = machine.Id,
            reportedByOperatorId = (Guid?)null,
            reportedAt = DateTime.UtcNow,
            goodQuantity = 80m,
            scrapQuantity = 20m,
            notes = (string?)null
        });
        confirmResponse.EnsureSuccessStatusCode();
        var confirmation = await ReadAsync<ProductionConfirmationDto>(confirmResponse);
        var toUtc = confirmation.ReportedAt.AddMinutes(1);

        var trendResponse = await client.GetAsync(
            $"{TrendUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}&idealCycleTimeSeconds=60&bucket=Day");

        trendResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var trend = await ReadAsync<OeeTrendDto>(trendResponse);
        trend.MachineId.Should().Be(machine.Id);
        trend.Bucket.Should().Be("Day");
        trend.Entries.Should().HaveCountGreaterThan(1);
        trend.Entries.Should().BeInAscendingOrder(e => e.FromUtc);
        trend.Entries.First().FromUtc.Should().Be(fromUtc.ToUniversalTime());
        trend.Entries.Last().ToUtc.Should().Be(toUtc.ToUniversalTime());

        // Every bucket matches a direct snapshot over the same window.
        foreach (var entry in trend.Entries)
        {
            var snapshotResponse = await client.GetAsync(
                $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(entry.FromUtc)}&toUtc={Qs(entry.ToUtc)}&idealCycleTimeSeconds=60");
            snapshotResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var snapshot = await ReadAsync<OeeSnapshotDto>(snapshotResponse);
            snapshot.Should().Be(entry);
        }
    }

    [Fact]
    public async Task Trend_EmptyBuckets_CarryNullFactors()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var fromUtc = DateTime.UtcNow.AddHours(-50);
        var toUtc = DateTime.UtcNow.AddMinutes(1);
        var trendResponse = await client.GetAsync(
            $"{TrendUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}&idealCycleTimeSeconds=60&bucket=Day");

        trendResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var trend = await ReadAsync<OeeTrendDto>(trendResponse);
        trend.Entries.Should().HaveCountGreaterThan(1);
        trend.Entries.Should().OnlyContain(e =>
            e.Availability == null && e.Performance == null && e.Quality == null && e.Oee == null
            && !e.AvailabilityComputed && !e.PerformanceComputed && !e.QualityComputed);
    }

    [Theory]
    [InlineData("Month")]
    [InlineData("")]
    public async Task Trend_UnknownBucket_Returns400(string bucket)
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var response = await client.GetAsync(
            $"{TrendUrl}?machineId={machine.Id}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}&idealCycleTimeSeconds=60&bucket={bucket}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Trend_ReversedWindow_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var response = await client.GetAsync(
            $"{TrendUrl}?machineId={machine.Id}&fromUtc={Qs(DateTime.UtcNow)}&toUtc={Qs(DateTime.UtcNow.AddHours(-8))}&idealCycleTimeSeconds=60&bucket=Day");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Trend_WindowOver93Days_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var response = await client.GetAsync(
            $"{TrendUrl}?machineId={machine.Id}&fromUtc={Qs(DateTime.UtcNow.AddDays(-100))}&toUtc={Qs(DateTime.UtcNow)}&idealCycleTimeSeconds=60&bucket=Week");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Trend_UnknownMachine_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync(
            $"{TrendUrl}?machineId={Guid.NewGuid()}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}&idealCycleTimeSeconds=60&bucket=Day");

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
            $"{TrendUrl}?machineId={foreignMachine.Id}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}&idealCycleTimeSeconds=60&bucket=Day");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Losses_HappyPath_ParetoTotalsMatchSnapshotAndScrap()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var fromUtc = DateTime.UtcNow.AddHours(-8);
        var machine = await CreateMachineAsync(client);
        await PutFullDayCalendarAsync(client, machine.Id, fromUtc, DateTime.UtcNow);
        var reasonA = await CreateReasonWithDtoAsync(client);
        var reasonB = await CreateReasonWithDtoAsync(client);

        var firstStart = DateTime.UtcNow.AddHours(-6);
        await StartAndCloseDowntimeAsync(client, machine.Id, reasonA.Id, firstStart, firstStart.AddMinutes(60));
        var secondStart = DateTime.UtcNow.AddHours(-4);
        await StartAndCloseDowntimeAsync(client, machine.Id, reasonB.Id, secondStart, secondStart.AddMinutes(30));

        var firstScrapAt = DateTime.UtcNow.AddHours(-3);
        await CreateScrapAsync(client, machine.Id, reasonA.Id, 10m, firstScrapAt);
        await CreateScrapAsync(client, machine.Id, reasonA.Id, 5m, firstScrapAt.AddMinutes(5));
        await CreateScrapAsync(client, machine.Id, reasonB.Id, 5m, firstScrapAt.AddMinutes(10));

        var toUtc = DateTime.UtcNow.AddMinutes(1);
        var lossesResponse = await client.GetAsync(
            $"{LossesUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}");

        lossesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var losses = await ReadAsync<OeeLossesDto>(lossesResponse);

        losses.MachineId.Should().Be(machine.Id);
        losses.TotalDowntimeMinutes.Should().BeApproximately(90, 0.5);
        losses.TotalScrapQuantity.Should().Be(20m);
        losses.DowntimePareto.Should().HaveCount(2);
        losses.DowntimePareto.Should().BeInDescendingOrder(e => e.Minutes);
        losses.DowntimePareto.Sum(e => e.Minutes).Should().BeApproximately(losses.TotalDowntimeMinutes, 0.001);
        losses.DowntimePareto.Sum(e => e.Share).Should().BeApproximately(1.0, 0.0001);
        var downtimeA = losses.DowntimePareto.Should().ContainSingle(e => e.ReasonCodeId == reasonA.Id).Subject;
        downtimeA.Code.Should().Be(reasonA.Code);
        downtimeA.Name.Should().Be(reasonA.Name);
        downtimeA.Minutes.Should().BeApproximately(60, 0.5);
        downtimeA.Share.Should().BeApproximately(0.6667, 0.0001);
        var downtimeB = losses.DowntimePareto.Should().ContainSingle(e => e.ReasonCodeId == reasonB.Id).Subject;
        downtimeB.Code.Should().Be(reasonB.Code);
        downtimeB.Name.Should().Be(reasonB.Name);
        downtimeB.Minutes.Should().BeApproximately(30, 0.5);
        downtimeB.Share.Should().BeApproximately(0.3333, 0.0001);
        losses.ScrapPareto.Should().HaveCount(2);
        losses.ScrapPareto.Should().BeInDescendingOrder(e => e.Quantity);
        losses.ScrapPareto.Sum(e => e.Quantity).Should().Be(losses.TotalScrapQuantity);
        losses.ScrapPareto.Sum(e => e.Share).Should().BeApproximately(1.0, 0.0001);

        // Downtime minutes agree with the snapshot run-time loss for the caller tenant.
        var snapshotResponse = await client.GetAsync(
            $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}&idealCycleTimeSeconds=60");
        snapshotResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var snapshot = await ReadAsync<OeeSnapshotDto>(snapshotResponse);
        snapshot.DowntimeMinutes.Should().BeApproximately(losses.TotalDowntimeMinutes, 0.001);
    }

    [Fact]
    public async Task Losses_UnknownMachine_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync(
            $"{LossesUrl}?machineId={Guid.NewGuid()}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Losses_CrossTenantMachine_Returns404()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var foreignMachine = await CreateMachineAsync(otherTenantClient);

        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync(
            $"{LossesUrl}?machineId={foreignMachine.Id}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Losses_ReversedWindow_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var response = await client.GetAsync(
            $"{LossesUrl}?machineId={machine.Id}&fromUtc={Qs(DateTime.UtcNow)}&toUtc={Qs(DateTime.UtcNow.AddHours(-8))}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Losses_WindowOver93Days_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var response = await client.GetAsync(
            $"{LossesUrl}?machineId={machine.Id}&fromUtc={Qs(DateTime.UtcNow.AddDays(-100))}&toUtc={Qs(DateTime.UtcNow)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<MachineDto> CreateMachineAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/machines", new
        {
            code = $"OEE-{Guid.NewGuid():N}"[..12],
            name = "OEE Work Center",
            description = (string?)null,
            departmentId = (Guid?)null,
            isActive = true
        });

        response.EnsureSuccessStatusCode();
        return await ReadAsync<MachineDto>(response);
    }

    private static async Task<Guid> CreateReasonAsync(HttpClient client)
    {
        return (await CreateReasonWithDtoAsync(client)).Id;
    }

    private static async Task<ReasonCodeDto> CreateReasonWithDtoAsync(HttpClient client)
    {
        var code = $"OEE-{Guid.NewGuid():N}"[..12];
        var response = await client.PostAsJsonAsync("/api/reason-codes", new
        {
            code,
            name = code,
            description = (string?)null,
            category = 1,
            isActive = true,
            sortIndex = 0
        });

        response.EnsureSuccessStatusCode();
        return await ReadAsync<ReasonCodeDto>(response);
    }

    private static async Task CreateScrapAsync(
        HttpClient client, Guid machineId, Guid reasonId, decimal quantity, DateTime reportedAt)
    {
        var response = await client.PostAsJsonAsync("/api/scrap-events", new
        {
            machineId,
            reasonCodeId = reasonId,
            quantity,
            reportedAt,
            notes = (string?)null,
            reportedByOperatorId = (Guid?)null,
            productionOrderId = (Guid?)null
        });

        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Covers the window weekdays with full-day working entries so planned
    /// time equals the window length regardless of the hour the test runs at.
    /// </summary>
    private static async Task PutFullDayCalendarAsync(HttpClient client, Guid machineId, DateTime from, DateTime to)
    {
        var days = new List<DayOfWeek>();
        for (var day = from.ToUniversalTime().Date; day <= to.ToUniversalTime().Date; day = day.AddDays(1))
        {
            if (!days.Contains(day.DayOfWeek))
                days.Add(day.DayOfWeek);
        }
        var response = await client.PutAsJsonAsync($"/api/machines/{machineId}/calendar", new
        {
            entries = days.Select(day => new
            {
                dayOfWeek = (int)day,
                startTime = "00:00:00",
                endTime = "00:00:00",
                shiftId = (Guid?)null,
                isWorking = true
            }).ToArray()
        });

        response.EnsureSuccessStatusCode();
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

    private static string UniqueCode() => $"PO-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    /// <summary>
    /// Builds a real recipe with one operation, releases its version, creates an
    /// order against it and releases the order.
    /// </summary>
    private static async Task<ProductionOrderDto> CreateReleasedOrderAsync(HttpClient client)
    {
        var recipe = await CreateRecipeAsync(client);
        var versionId = recipe.Versions.Single().Id;

        var operationResponse = await client.PostAsJsonAsync("/api/operations", new
        {
            versionId,
            code = $"OP-{Guid.NewGuid():N}"[..8],
            name = "Cutting",
            description = (string?)null,
            operationType = (string?)null,
            sortIndex = 0,
            setupTimeMinutes = (decimal?)null,
            runTimeMode = 1,
            runTimePerUnitSeconds = 10m,
            runTimePerBatchMinutes = (decimal?)null,
            teardownTimeMinutes = (decimal?)null,
            queueTimeMinutes = (decimal?)null,
            isOptional = false,
            allowParallelExecution = false,
            expectedQuantity = (decimal?)null
        });
        operationResponse.EnsureSuccessStatusCode();

        var releaseVersionResponse = await client.PostAsync($"/api/recipe-versions/{versionId}/release", null);
        releaseVersionResponse.EnsureSuccessStatusCode();

        var order = await CreateOrderAsync(client, recipe.Id, versionId);

        var releaseOrderResponse = await client.PostAsync($"/api/production-orders/{order.Id}/release", null);
        releaseOrderResponse.EnsureSuccessStatusCode();
        return await ReadAsync<ProductionOrderDto>(releaseOrderResponse);
    }

    private static async Task<ProductionOrderDto> CreateOrderAsync(
        HttpClient client, Guid? recipeId = null, Guid? recipeVersionId = null)
    {
        var response = await client.PostAsJsonAsync("/api/production-orders", new
        {
            code = UniqueCode(),
            productId = Guid.NewGuid(),
            recipeId = recipeId ?? Guid.NewGuid(),
            recipeVersionId = recipeVersionId ?? Guid.NewGuid(),
            plannedQuantity = 100m,
            measureUnitId = (Guid?)null,
            priority = 0,
            dueDate = (DateTime?)null,
            notes = (string?)null,
            syncId = (string?)null
        });

        response.EnsureSuccessStatusCode();
        return await ReadAsync<ProductionOrderDto>(response);
    }

    private static async Task<RecipeDto> CreateRecipeAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/recipes", new
        {
            code = $"R-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            name = "Integration recipe",
            description = (string?)null,
            isActive = true,
            primaryProductId = (Guid?)null,
            syncId = (string?)null
        });
        response.EnsureSuccessStatusCode();
        return await ReadAsync<RecipeDto>(response);
    }
}
