using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>/api/schedule/gantt</c> - the
/// time-phased Gantt schedule read-model over Released Production Orders
/// (issue #304, slice 1/3). They exercise the full request pipeline (auth,
/// tenant resolution, computed scheduling, manual-override overlay and the
/// global exception handler) against a real PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class GanttScheduleEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/schedule/gantt";

    // A window no other test class seeds into, so bars stay ours.
    private const string From = "2027-05-10";
    private const string To = "2027-05-12";

    [Fact]
    public async Task GetGantt_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync($"{BaseUrl}?from={From}&to={To}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetGantt_MissingDates_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetGantt_ReversedWindow_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{BaseUrl}?from={To}&to={From}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetGantt_WindowBoundaries_32Days400_31Days200()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var tooWide = await client.GetAsync($"{BaseUrl}?from=2027-05-01&to=2027-06-01");

        tooWide.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var maxWide = await client.GetAsync($"{BaseUrl}?from=2027-05-01&to=2027-05-31");

        maxWide.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<GanttScheduleDto>(maxWide)).Groups.Should().NotBeNull();
    }

    [Fact]
    public async Task GetGantt_HappyPath_ReturnsPerWorkCenterBarsMatchingTimingMath()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = UniqueTag();
        var machine = await CreateMachineAsync(client, $"GNT-{tag}-WC");
        var recipe = await CreateRecipeAsync(client);

        // Two chained ops: 60s/unit x 60 units = 60min each. B starts when A
        // ends (FinishToStart, no lag).
        var opA = await CreateOperationAsync(client, recipe.Versions.Single().Id, "A", perUnitSeconds: 60m);
        var opB = await CreateOperationAsync(client, recipe.Versions.Single().Id, "B", perUnitSeconds: 60m);
        await SetDependenciesAsync(client, opB, opA);
        await AddResourceAsync(client, opA, machine.Id);
        await AddResourceAsync(client, opB, machine.Id);
        await ReleaseVersionAsync(client, recipe.Versions.Single().Id);

        var dueDate = new DateTime(2027, 5, 11, 12, 0, 0, DateTimeKind.Utc);
        var order = await CreateReleasedOrderAsync(client, tag, "FIX", recipe, plannedQuantity: 60m, dueDate: dueDate);

        var response = await client.GetAsync($"{BaseUrl}?from={From}&to={To}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var schedule = await ReadAsync<GanttScheduleDto>(response);

        schedule.From.Should().Be(From);
        schedule.To.Should().Be(To);

        // Other test classes share the dev tenant, so scope every assertion
        // to the rows seeded here (same pattern as DispatchBoardEndpointTests).
        var group = schedule.Groups.Should().ContainSingle(g => g.MachineCode == $"GNT-{tag}-WC").Subject;
        group.MachineId.Should().Be(machine.Id);
        var bars = group.Bars.Where(b => b.ProductionOrderCode == order.Code).ToList();
        bars.Should().HaveCount(2);

        // Backward-anchored so the 120min chain ends exactly at the due date.
        var barA = bars.Single(b => b.OperationCode.EndsWith("-A"));
        barA.ProductionOrderId.Should().Be(order.Id);
        barA.ProductionOrderCode.Should().Be(order.Code);
        barA.PlannedStart.Should().Be(dueDate.AddMinutes(-120));
        barA.PlannedEnd.Should().Be(dueDate.AddMinutes(-60));
        barA.IsOverdue.Should().BeFalse();

        var barB = bars.Single(b => b.OperationCode.EndsWith("-B"));
        barB.PlannedStart.Should().Be(dueDate.AddMinutes(-60));
        barB.PlannedEnd.Should().Be(dueDate);
        barB.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public async Task GetGantt_MachineFilter_ReturnsOnlyMatchingLane()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = UniqueTag();
        var keep = await CreateMachineAsync(client, $"GNT-{tag}-KEEP");
        var skip = await CreateMachineAsync(client, $"GNT-{tag}-SKIP");
        var recipe = await CreateRecipeAsync(client);
        var opA = await CreateOperationAsync(client, recipe.Versions.Single().Id, "A", perUnitSeconds: 60m);
        var opB = await CreateOperationAsync(client, recipe.Versions.Single().Id, "B", perUnitSeconds: 60m);
        await SetDependenciesAsync(client, opB, opA);
        await AddResourceAsync(client, opA, keep.Id);
        await AddResourceAsync(client, opB, skip.Id);
        await ReleaseVersionAsync(client, recipe.Versions.Single().Id);
        await CreateReleasedOrderAsync(
            client, tag, "FLT", recipe, plannedQuantity: 60m,
            dueDate: new DateTime(2027, 5, 11, 12, 0, 0, DateTimeKind.Utc));
        var orderCode = $"GNT-{tag}-FLT";

        var response = await client.GetAsync($"{BaseUrl}?from={From}&to={To}&machineId={keep.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var schedule = await ReadAsync<GanttScheduleDto>(response);

        // The shared dev tenant may hold other lanes; scope to ours.
        var group = schedule.Groups.Should().ContainSingle(g => g.MachineId == keep.Id).Subject;
        group.Bars.Where(b => b.ProductionOrderCode == orderCode)
            .Should().ContainSingle().Which.OperationCode.Should().EndWith("-A");
    }

    [Fact]
    public async Task GetGantt_OrdersAndMachinesOfAnotherTenant_AreNotVisible()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var tag = UniqueTag();
        var foreignMachine = await CreateMachineAsync(otherTenantClient, $"GNT-{tag}-WC");
        var recipe = await CreateRecipeAsync(otherTenantClient);
        var op = await CreateOperationAsync(otherTenantClient, recipe.Versions.Single().Id, "A", perUnitSeconds: 60m);
        await AddResourceAsync(otherTenantClient, op, foreignMachine.Id);
        await ReleaseVersionAsync(otherTenantClient, recipe.Versions.Single().Id);
        var foreignOrder = await CreateReleasedOrderAsync(
            otherTenantClient, tag, "FRN", recipe, plannedQuantity: 60m,
            dueDate: new DateTime(2027, 5, 11, 12, 0, 0, DateTimeKind.Utc));

        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync($"{BaseUrl}?from={From}&to={To}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var schedule = await ReadAsync<GanttScheduleDto>(response);

        schedule.Groups.SelectMany(g => g.Bars).Select(b => b.ProductionOrderCode)
            .Should().NotContain(foreignOrder.Code);
        schedule.Groups.Select(g => g.MachineCode).Should().NotContain($"GNT-{tag}-WC");
    }

    private static string UniqueTag() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static async Task<MachineDto> CreateMachineAsync(HttpClient client, string code)
    {
        var response = await client.PostAsJsonAsync("/api/machines", new
        {
            code,
            name = code,
            description = (string?)null,
            isActive = true
        });
        response.EnsureSuccessStatusCode();
        return await ReadAsync<MachineDto>(response);
    }

    private static async Task<RecipeDto> CreateRecipeAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/recipes", new
        {
            code = $"R-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            name = "Gantt recipe",
            description = (string?)null,
            isActive = true,
            primaryProductId = (Guid?)null,
            syncId = (string?)null
        });
        response.EnsureSuccessStatusCode();
        return await ReadAsync<RecipeDto>(response);
    }

    private static async Task<OperationDto> CreateOperationAsync(
        HttpClient client, Guid versionId, string suffix, decimal perUnitSeconds)
    {
        var response = await client.PostAsJsonAsync("/api/operations", new
        {
            versionId,
            code = $"OP-{Guid.NewGuid():N}"[..8].ToUpperInvariant() + $"-{suffix}",
            name = $"Operation {suffix}",
            description = (string?)null,
            operationType = (string?)null,
            sortIndex = suffix == "A" ? 0 : 1,
            setupTimeMinutes = (decimal?)null,
            runTimeMode = 1,
            runTimePerUnitSeconds = perUnitSeconds,
            runTimePerBatchMinutes = (decimal?)null,
            teardownTimeMinutes = (decimal?)null,
            queueTimeMinutes = (decimal?)null,
            isOptional = false,
            allowParallelExecution = false,
            expectedQuantity = (decimal?)null
        });
        response.EnsureSuccessStatusCode();
        return await ReadAsync<OperationDto>(response);
    }

    private static async Task SetDependenciesAsync(HttpClient client, OperationDto successor, OperationDto predecessor)
    {
        var response = await client.PutAsJsonAsync($"/api/operations/{successor.Id}/dependencies", new
        {
            dependencies = new[]
            {
                new { predecessorOperationId = predecessor.Id, dependencyType = 1, lagMinutes = (decimal?)null }
            }
        });
        response.EnsureSuccessStatusCode();
    }

    private static async Task AddResourceAsync(HttpClient client, OperationDto operation, Guid machineId)
    {
        var response = await client.PostAsJsonAsync($"/api/operations/{operation.Id}/resources", new
        {
            operationId = operation.Id,
            preferredDepartmentId = (Guid?)null,
            preferredMachineId = machineId,
            requiredCapability = (string?)null,
            requiredOperatorCount = 1,
            requiredRole = (string?)null,
            notes = (string?)null
        });
        response.EnsureSuccessStatusCode();
    }

    private static async Task ReleaseVersionAsync(HttpClient client, Guid versionId)
    {
        var response = await client.PostAsync($"/api/recipe-versions/{versionId}/release", null);
        response.EnsureSuccessStatusCode();
    }

    private async Task<ProductionOrderDto> CreateReleasedOrderAsync(
        HttpClient client, string tag, string suffix, RecipeDto recipe, decimal plannedQuantity, DateTime? dueDate)
    {
        var orderResponse = await client.PostAsJsonAsync("/api/production-orders", new
        {
            code = $"GNT-{tag}-{suffix}",
            productId = Guid.NewGuid(),
            recipeId = recipe.Id,
            recipeVersionId = recipe.Versions.Single().Id,
            plannedQuantity,
            measureUnitId = (Guid?)null,
            priority = 0,
            dueDate,
            notes = (string?)null,
            syncId = (string?)null
        });
        orderResponse.EnsureSuccessStatusCode();
        var order = await ReadAsync<ProductionOrderDto>(orderResponse);

        var releaseResponse = await client.PostAsync($"/api/production-orders/{order.Id}/release", null);
        releaseResponse.EnsureSuccessStatusCode();
        return await ReadAsync<ProductionOrderDto>(releaseResponse);
    }

    private sealed record OperationDto(Guid Id, string Code);
}
