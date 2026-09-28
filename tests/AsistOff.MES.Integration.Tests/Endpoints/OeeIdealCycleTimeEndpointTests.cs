using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for routing-resolved OEE ideal cycle
/// time (issue #399): <c>GET /api/oee/snapshot</c> and
/// <c>GET /api/oee/trend</c> accept an omitted
/// <c>idealCycleTimeSeconds</c> and resolve it from the confirmed orders'
/// operations (same minimum-positive rule as the summary endpoint).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class OeeIdealCycleTimeEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string SnapshotUrl = "/api/oee/snapshot";
    private const string TrendUrl = "/api/oee/trend";
    private const string SummaryUrl = "/api/oee";

    [Fact]
    public async Task Snapshot_WithoutParam_ResolvesFromRouting_MatchesSummary()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var fromUtc = DateTime.UtcNow.AddHours(-8);
        var machine = await CreateMachineAsync(client);
        await PutFullDayCalendarAsync(client, machine.Id, fromUtc, DateTime.UtcNow);
        var order = await CreateReleasedOrderAsync(client, runTimePerUnitSeconds: 10m);

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

        var snapshotResponse = await client.GetAsync(
            $"{SnapshotUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}");

        snapshotResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var snapshot = await ReadAsync<OeeSnapshotDto>(snapshotResponse);
        snapshot.IdealCycleTimeSeconds.Should().Be(10m);
        snapshot.IdealCycleTimeSource.Should().Be("routing");

        var summaryResponse = await client.GetAsync(
            $"{SummaryUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}");
        summaryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await ReadAsync<OeeSummaryDto>(summaryResponse);
        summary.IdealCycleTimeSeconds.Should().Be(10m);
        snapshot.Performance.Should().Be(summary.Performance);
    }

    [Fact]
    public async Task Snapshot_ExplicitParam_OverridesRouting_AndIsEchoed()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var fromUtc = DateTime.UtcNow.AddHours(-8);
        var machine = await CreateMachineAsync(client);
        await PutFullDayCalendarAsync(client, machine.Id, fromUtc, DateTime.UtcNow);
        var order = await CreateReleasedOrderAsync(client, runTimePerUnitSeconds: 10m);

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
            $"{SnapshotUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}&idealCycleTimeSeconds=60");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var snapshot = await ReadAsync<OeeSnapshotDto>(response);
        snapshot.IdealCycleTimeSeconds.Should().Be(60m);
        snapshot.IdealCycleTimeSource.Should().Be("caller");
    }

    [Fact]
    public async Task Snapshot_WithoutParam_AndNoConfirmations_Returns400_NamingIdeal()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var response = await client.GetAsync(
            $"{SnapshotUrl}?machineId={machine.Id}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("idealCycleTimeSeconds");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    public async Task Snapshot_NonPositiveParam_Returns400(string ideal)
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var response = await client.GetAsync(
            $"{SnapshotUrl}?machineId={machine.Id}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}&idealCycleTimeSeconds={ideal}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Snapshot_CrossTenantMachine_WithoutParam_Returns404()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var foreignMachine = await CreateMachineAsync(otherTenantClient);

        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync(
            $"{SnapshotUrl}?machineId={foreignMachine.Id}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Trend_WithoutParam_ResolvesFromRouting_MatchesSummary()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var fromUtc = DateTime.UtcNow.AddHours(-8);
        var machine = await CreateMachineAsync(client);
        await PutFullDayCalendarAsync(client, machine.Id, fromUtc, DateTime.UtcNow);
        var order = await CreateReleasedOrderAsync(client, runTimePerUnitSeconds: 10m);

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

        var trendResponse = await client.GetAsync(
            $"{TrendUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}&bucket=Day");

        trendResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var trend = await ReadAsync<OeeTrendDto>(trendResponse);
        trend.IdealCycleTimeSeconds.Should().Be(10m);
        trend.IdealCycleTimeSource.Should().Be("routing");
        trend.Buckets.Should().NotBeEmpty();
        trend.Buckets.Should().OnlyContain(b => b.IdealCycleTimeSource == "routing");

        var summaryResponse = await client.GetAsync(
            $"{SummaryUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}");
        summaryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await ReadAsync<OeeSummaryDto>(summaryResponse);
        trend.IdealCycleTimeSeconds.Should().Be(summary.IdealCycleTimeSeconds);
    }

    [Fact]
    public async Task Trend_ExplicitParam_OverridesRouting_AndIsEchoed()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var fromUtc = DateTime.UtcNow.AddHours(-8);
        var machine = await CreateMachineAsync(client);
        await PutFullDayCalendarAsync(client, machine.Id, fromUtc, DateTime.UtcNow);
        var order = await CreateReleasedOrderAsync(client, runTimePerUnitSeconds: 10m);

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
            $"{TrendUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}&idealCycleTimeSeconds=60&bucket=Day");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var trend = await ReadAsync<OeeTrendDto>(response);
        trend.IdealCycleTimeSeconds.Should().Be(60m);
        trend.IdealCycleTimeSource.Should().Be("caller");
        trend.Buckets.Should().OnlyContain(b => b.IdealCycleTimeSeconds == 60m);
    }

    [Fact]
    public async Task Trend_WithoutParam_AndNoConfirmations_Returns400_NamingIdeal()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var response = await client.GetAsync(
            $"{TrendUrl}?machineId={machine.Id}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}&bucket=Day");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("idealCycleTimeSeconds");
    }

    [Fact]
    public async Task Trend_CrossTenantMachine_WithoutParam_Returns404()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var foreignMachine = await CreateMachineAsync(otherTenantClient);

        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync(
            $"{TrendUrl}?machineId={foreignMachine.Id}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}&bucket=Day");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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

    private static string UniqueCode() => $"PO-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    private static async Task<ProductionOrderDto> CreateReleasedOrderAsync(HttpClient client, decimal runTimePerUnitSeconds)
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
            runTimePerUnitSeconds,
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
