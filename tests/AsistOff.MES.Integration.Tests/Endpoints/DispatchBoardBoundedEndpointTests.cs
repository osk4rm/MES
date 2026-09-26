using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Hardening tests for issue #274: the dispatch board issues a bounded database
/// query returning at most 200 order rows with overdue first, without loading
/// the full order table. Seeds 250 Released orders, asserts the 200-row cap
/// with overdue-first ordering, cross-tenant isolation, and 400 window paths.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class DispatchBoardBoundedEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/schedule/dispatch";

    // Window isolated from other dispatch test classes.
    private const string From = "2028-05-10";
    private const string To = "2028-05-12";

    [Fact]
    public async Task GetDispatch_With250ReleasedOrders_Returns200WithOverdueFirst()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = UniqueTag();
        var recipe = await CreateRecipeAsync(client);
        var versionId = recipe.Versions.Single().Id;
        await CreateOperationAsync(client, versionId);
        await client.PostAsync($"/api/recipe-versions/{versionId}/release", null);

        var overdueDate = new DateTime(2028, 5, 8, 0, 0, 0, DateTimeKind.Utc);
        var inWindowDate = new DateTime(2028, 5, 11, 0, 0, 0, DateTimeKind.Utc);

        // Two overdue rows must surface first; the remaining 248 in-window rows
        // fill the page to the 200-row cap.
        var overdueFirst = await CreateReleasedOrderAsync(client, tag, "OVD-001", overdueDate, recipe.Id, versionId);
        var overdueSecond = await CreateReleasedOrderAsync(client, tag, "OVD-002", overdueDate, recipe.Id, versionId);
        for (var i = 0; i < 248; i++)
            await CreateReleasedOrderAsync(client, tag, $"W{i:000}", inWindowDate, recipe.Id, versionId);

        var response = await client.GetAsync($"{BaseUrl}?from={From}&to={To}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var board = await ReadAsync<DispatchBoardDto>(response);

        board.Orders.Should().HaveCount(200);
        var codes = board.Orders.Select(o => o.Code).ToList();
        codes.Should().Contain(overdueFirst.Code);
        codes.Should().Contain(overdueSecond.Code);
        codes.IndexOf(overdueFirst.Code).Should().BeLessThan(5);
        codes.IndexOf(overdueSecond.Code).Should().BeLessThan(5);
        board.Orders.Take(2).Should().OnlyContain(o => o.IsOverdue);
    }

    [Fact]
    public async Task GetDispatch_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync($"{BaseUrl}?from={From}&to={To}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDispatch_OverWideWindow_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{BaseUrl}?from=2028-05-01&to=2028-06-02");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetDispatch_ReversedWindow_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{BaseUrl}?from={To}&to={From}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetDispatch_CrossTenantOrder_NeverAppears()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var tag = UniqueTag();
        var recipe = await CreateRecipeAsync(otherTenantClient);
        var versionId = recipe.Versions.Single().Id;
        await CreateOperationAsync(otherTenantClient, versionId);
        await otherTenantClient.PostAsync($"/api/recipe-versions/{versionId}/release", null);
        var foreign = await CreateReleasedOrderAsync(
            otherTenantClient, tag, "FRN", new DateTime(2028, 5, 11, 0, 0, 0, DateTimeKind.Utc), recipe.Id, versionId);

        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync($"{BaseUrl}?from={From}&to={To}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<DispatchBoardDto>(response)).Orders
            .Select(o => o.Code).Should().NotContain(foreign.Code);
    }

    private static string UniqueTag() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static async Task<RecipeDto> CreateRecipeAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/recipes", new
        {
            code = $"R-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            name = "Bounded dispatch recipe",
            description = (string?)null,
            isActive = true,
            primaryProductId = (Guid?)null,
            syncId = (string?)null
        });
        response.EnsureSuccessStatusCode();
        return await ReadAsync<RecipeDto>(response);
    }

    private static async Task CreateOperationAsync(HttpClient client, Guid versionId)
    {
        var response = await client.PostAsJsonAsync("/api/operations", new
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
        response.EnsureSuccessStatusCode();
    }

    private static async Task<ProductionOrderDto> CreateReleasedOrderAsync(
        HttpClient client, string tag, string suffix, DateTime? dueDate, Guid recipeId, Guid versionId)
    {
        var orderResponse = await client.PostAsJsonAsync("/api/production-orders", new
        {
            code = $"BND-{tag}-{suffix}",
            productId = Guid.NewGuid(),
            recipeId,
            recipeVersionId = versionId,
            plannedQuantity = 100m,
            measureUnitId = (Guid?)null,
            priority = 0,
            dueDate,
            notes = (string?)null,
            syncId = (string?)null
        });
        orderResponse.EnsureSuccessStatusCode();
        var order = await ReadAsync<ProductionOrderDto>(orderResponse);

        var release = await client.PostAsync($"/api/production-orders/{order.Id}/release", null);
        release.EnsureSuccessStatusCode();
        return await ReadAsync<ProductionOrderDto>(release);
    }
}
