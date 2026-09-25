using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>/api/oee</c> (slice 1: Quality
/// factor plus raw confirmation counts). They exercise the full request
/// pipeline: authentication, tenant resolution, the read-time confirmation
/// aggregation and the global exception handler - against a real PostgreSQL
/// database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class OeeSummaryEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/oee";

    [Fact]
    public async Task Summary_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={Guid.NewGuid()}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Summary_HappyPath_ReturnsCountsAndQuality()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);
        var order = await CreateReleasedOrderAsync(client);
        var fromUtc = DateTime.UtcNow.AddHours(-8);

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
            $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await ReadAsync<OeeSummaryDto>(response);

        summary.MachineId.Should().Be(machine.Id);
        summary.FromUtc.Should().Be(fromUtc.ToUniversalTime());
        summary.ToUtc.Should().Be(toUtc.ToUniversalTime());
        summary.GoodCount.Should().Be(90m);
        summary.ScrapCount.Should().Be(10m);
        summary.TotalCount.Should().Be(100m);
        summary.Quality.Should().BeApproximately(0.9, 0.0001);
    }

    [Fact]
    public async Task Summary_EmptyPeriod_ReturnsZerosAndNullQuality()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await ReadAsync<OeeSummaryDto>(response);

        summary.GoodCount.Should().Be(0m);
        summary.ScrapCount.Should().Be(0m);
        summary.TotalCount.Should().Be(0m);
        summary.Quality.Should().BeNull();
    }

    [Fact]
    public async Task Summary_UnknownMachine_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={Guid.NewGuid()}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Summary_CrossTenantMachine_Returns404()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var foreignMachine = await CreateMachineAsync(otherTenantClient);

        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={foreignMachine.Id}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Summary_ReversedWindow_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var response = await client.GetAsync(
            $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(DateTime.UtcNow)}&toUtc={Qs(DateTime.UtcNow.AddHours(-8))}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Summary_MissingDates_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var missingTo = await client.GetAsync(
            $"{BaseUrl}?machineId={machine.Id}&fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}");

        missingTo.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
