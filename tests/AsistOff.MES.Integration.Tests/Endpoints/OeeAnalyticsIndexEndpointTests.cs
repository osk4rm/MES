using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for the OEE analytics index deliverable
/// (issue #266). They seed confirmations through the real API, then query the
/// OEE snapshot, trend, and summary for the same Work Center and window and
/// assert identical factors — proving the new
/// <c>(TenantId, MachineId, ReportedAt)</c> index changed the plan, not the
/// results. A second test plants a cross-tenant confirmation row carrying a
/// colliding Work Center id and proves the window query never leaks it.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class OeeAnalyticsIndexEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string SummaryUrl = "/api/oee";
    private const string SnapshotUrl = "/api/oee/snapshot";
    private const string TrendUrl = "/api/oee/trend";

    [Fact]
    public async Task Snapshot_Trend_Summary_ReturnIdenticalFactors_ForSameWindow()
    {
        // Arrange
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

        // Act
        var summaryResponse = await client.GetAsync(
            $"{SummaryUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}");
        summaryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await ReadAsync<OeeSummaryDto>(summaryResponse);

        summary.IdealCycleTimeSeconds.Should().Be(10m);

        var snapshotResponse = await client.GetAsync(
            $"{SnapshotUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}&idealCycleTimeSeconds={summary.IdealCycleTimeSeconds}");
        snapshotResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var snapshot = await ReadAsync<OeeSnapshotDto>(snapshotResponse);

        var trendResponse = await client.GetAsync(
            $"{TrendUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}&idealCycleTimeSeconds={summary.IdealCycleTimeSeconds}&bucket=Day");
        trendResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var trend = await ReadAsync<OeeTrendDto>(trendResponse);

        // Assert: snapshot and summary agree on every factor for the window.
        // Raw performance here (?0.04) sits far below the summary 1.0 clamp,
        // so the clamped and unclamped formulas coincide.
        snapshot.MachineId.Should().Be(machine.Id);
        snapshot.TotalCount.Should().Be(summary.TotalCount);
        snapshot.GoodCount.Should().Be(summary.GoodCount);
        snapshot.ScrapCount.Should().Be(summary.ScrapCount);
        snapshot.Quality.Should().BeApproximately(summary.Quality!.Value, 0.0001);
        snapshot.Availability.Should().BeApproximately(summary.Availability!.Value, 0.0001);
        snapshot.Performance.Should().BeApproximately(summary.Performance!.Value, 0.0001);
        snapshot.Oee.Should().BeApproximately(summary.Oee!.Value, 0.0001);

        // Assert: trend buckets partition the window, so their totals sum to
        // the whole-window snapshot (and hence to the summary).
        trend.Buckets.Should().NotBeEmpty();
        trend.Buckets.First().FromUtc.Should().Be(fromUtc.ToUniversalTime());
        trend.Buckets.Last().ToUtc.Should().Be(toUtc.ToUniversalTime());
        trend.Buckets.Select(b => b.TotalCount).Sum().Should().Be(snapshot.TotalCount);
        trend.Buckets.Select(b => b.GoodCount).Sum().Should().Be(snapshot.GoodCount);
        trend.Buckets.Select(b => b.ScrapCount).Sum().Should().Be(snapshot.ScrapCount);
        trend.Buckets.Select(b => b.PlannedProductionTimeMinutes).Sum()
            .Should().BeApproximately(snapshot.PlannedProductionTimeMinutes, 0.001);
        trend.Buckets.Select(b => b.DowntimeMinutes).Sum()
            .Should().BeApproximately(snapshot.DowntimeMinutes, 0.001);
    }

    [Fact]
    public async Task CrossTenant_ConfirmationRows_NeverLeakIntoWindowQuery()
    {
        // Arrange: tenant A owns the Work Center, its calendar, order, and
        // one confirmation (90 good + 10 scrap).
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var fromUtc = DateTime.UtcNow.AddHours(-8);
        var machine = await CreateMachineAsync(client);
        await PutFullDayCalendarAsync(client, machine.Id, fromUtc, DateTime.UtcNow);
        var order = await CreateReleasedOrderAsync(client);

        var ownConfirmResponse = await client.PostAsJsonAsync("/api/production-confirmations", new
        {
            productionOrderId = order.Id,
            machineId = machine.Id,
            reportedByOperatorId = (Guid?)null,
            reportedAt = DateTime.UtcNow,
            goodQuantity = 90m,
            scrapQuantity = 10m,
            notes = (string?)null
        });
        ownConfirmResponse.EnsureSuccessStatusCode();
        var ownConfirmation = await ReadAsync<ProductionConfirmationDto>(ownConfirmResponse);
        var toUtc = ownConfirmation.ReportedAt.AddMinutes(1);

        // A second tenant plants a much larger confirmation row carrying the
        // same Work Center id (MachineId is a loose Guid, so the API accepts
        // it against the foreign tenant's own order).
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var foreignOrder = await CreateReleasedOrderAsync(otherTenantClient);

        var foreignConfirmResponse = await otherTenantClient.PostAsJsonAsync("/api/production-confirmations", new
        {
            productionOrderId = foreignOrder.Id,
            machineId = machine.Id,
            reportedByOperatorId = (Guid?)null,
            reportedAt = DateTime.UtcNow,
            goodQuantity = 1000m,
            scrapQuantity = 500m,
            notes = (string?)null
        });
        foreignConfirmResponse.EnsureSuccessStatusCode();

        // Act
        var snapshotResponse = await client.GetAsync(
            $"{SnapshotUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}&idealCycleTimeSeconds=60");
        snapshotResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var snapshot = await ReadAsync<OeeSnapshotDto>(snapshotResponse);

        var summaryResponse = await client.GetAsync(
            $"{SummaryUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}");
        summaryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await ReadAsync<OeeSummaryDto>(summaryResponse);

        // Assert: only the own-tenant confirmation feeds the window query.
        // Any leak would surface as 1600 total / 1090 good / 510 scrap.
        snapshot.TotalCount.Should().Be(100m);
        snapshot.GoodCount.Should().Be(90m);
        snapshot.ScrapCount.Should().Be(10m);
        snapshot.Quality.Should().BeApproximately(0.9, 0.0001);

        summary.TotalCount.Should().Be(100m);
        summary.GoodCount.Should().Be(90m);
        summary.ScrapCount.Should().Be(10m);
        summary.Quality.Should().BeApproximately(0.9, 0.0001);

        // Assert: the foreign Work Center id itself stays invisible — the
        // tenant filter maps it to 404 rather than leaking rows.
        var crossTenantResponse = await client.GetAsync(
            $"{SnapshotUrl}?machineId={Guid.NewGuid()}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}&idealCycleTimeSeconds=60");
        crossTenantResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static string Qs(DateTime value) => Uri.EscapeDataString(value.ToString("O"));

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
        var code = $"OEE-{Guid.NewGuid():N}"[..12];
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

    /// <summary>
    /// Covers the window weekdays with full-day working entries so planned
    /// time equals the window length regardless of the hour the test runs at.
    /// </summary>
    private static async Task PutFullDayCalendarAsync(HttpClient client, Guid machineId, DateTime from, DateTime to)
    {
        var days = new[] { from.DayOfWeek, to.DayOfWeek }.Distinct().ToList();
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
    /// Builds a real recipe with one operation (10s ideal), releases its
    /// version, creates an order against it and releases the order.
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
