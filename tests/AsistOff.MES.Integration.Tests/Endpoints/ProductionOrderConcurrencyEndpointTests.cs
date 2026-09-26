using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Optimistic-concurrency integration tests for <c>/api/production-orders</c>
/// (issue #263). They prove the token round-trips through the real HTTP
/// pipeline against a real PostgreSQL: <c>xmin</c> changes on every write,
/// stale tokens are rejected with 409 carrying the current token, concurrent
/// writers never lose an update, and cross-tenant reads never leak the token.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ProductionOrderConcurrencyEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/production-orders";
    private const string ConfirmationsUrl = "/api/production-confirmations";

    [Fact]
    public async Task Get_ReturnsToken_ThatChangesOnEveryWrite()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var created = await CreateOrderAsync(client);
        created.ConcurrencyToken.Should().NotBeNullOrWhiteSpace();

        var fetched = await ReadAsync<ProductionOrderDto>(await client.GetAsync($"{BaseUrl}/{created.Id}"));
        fetched.ConcurrencyToken.Should().Be(created.ConcurrencyToken);

        var updated = await ReadAsync<ProductionOrderDto>(
            await client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", UpdatePayload(created, created.ConcurrencyToken, 101m)));

        updated.ConcurrencyToken.Should().NotBeNullOrWhiteSpace();
        updated.ConcurrencyToken.Should().NotBe(created.ConcurrencyToken);

        var refetched = await ReadAsync<ProductionOrderDto>(await client.GetAsync($"{BaseUrl}/{created.Id}"));
        refetched.ConcurrencyToken.Should().Be(updated.ConcurrencyToken);
    }

    [Fact]
    public async Task Update_WithStaleToken_Returns409WithCurrentToken_AndKeepsWinnerValues()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var created = await CreateOrderAsync(client);

        var first = await client.PutAsJsonAsync(
            $"{BaseUrl}/{created.Id}", UpdatePayload(created, created.ConcurrencyToken, 111m));
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var winner = await ReadAsync<ProductionOrderDto>(first);

        var replay = await client.PutAsJsonAsync(
            $"{BaseUrl}/{created.Id}", UpdatePayload(created, created.ConcurrencyToken, 222m));

        replay.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await ReadAsync<JsonDocument>(replay);
        problem.RootElement.GetProperty("concurrencyToken").GetString().Should().Be(winner.ConcurrencyToken);

        var fetched = await ReadAsync<ProductionOrderDto>(await client.GetAsync($"{BaseUrl}/{created.Id}"));
        fetched.PlannedQuantity.Should().Be(111m);
        fetched.ConcurrencyToken.Should().Be(winner.ConcurrencyToken);
    }

    [Fact]
    public async Task Update_WithoutToken_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var created = await CreateOrderAsync(client);

        var response = await client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", new
        {
            id = created.Id,
            code = created.Code,
            productId = created.ProductId,
            recipeId = created.RecipeId,
            recipeVersionId = created.RecipeVersionId,
            plannedQuantity = 999m,
            measureUnitId = (Guid?)null,
            priority = 0,
            dueDate = (DateTime?)null,
            notes = (string?)null,
            syncId = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ConcurrentUpdates_YieldExactlyOneSuccessAndOneConflict()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var created = await CreateOrderAsync(client);

        var firstCall = client.PutAsJsonAsync(
            $"{BaseUrl}/{created.Id}", UpdatePayload(created, created.ConcurrencyToken, 111m));
        var secondCall = client.PutAsJsonAsync(
            $"{BaseUrl}/{created.Id}", UpdatePayload(created, created.ConcurrencyToken, 222m));

        var responses = await Task.WhenAll(firstCall, secondCall);

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(1);

        var winner = await ReadAsync<ProductionOrderDto>(responses.Single(r => r.StatusCode == HttpStatusCode.OK));
        var fetched = await ReadAsync<ProductionOrderDto>(await client.GetAsync($"{BaseUrl}/{created.Id}"));
        fetched.PlannedQuantity.Should().Be(winner.PlannedQuantity);
        fetched.ConcurrencyToken.Should().Be(winner.ConcurrencyToken);
    }

    [Fact]
    public async Task Release_WithStaleToken_Returns409_AndFreshTokenSucceeds()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasableOrderAsync(client);

        // Advance the token with an unrelated edit while the order stays Planned.
        var edited = await ReadAsync<ProductionOrderDto>(
            await client.PutAsJsonAsync($"{BaseUrl}/{order.Id}", UpdatePayload(order, order.ConcurrencyToken, 150m)));
        edited.ConcurrencyToken.Should().NotBe(order.ConcurrencyToken);

        var stale = await client.PostAsync($"{BaseUrl}/{order.Id}/release?concurrencyToken={order.ConcurrencyToken}", null);
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await ReadAsync<JsonDocument>(stale);
        problem.RootElement.GetProperty("concurrencyToken").GetString().Should().Be(edited.ConcurrencyToken);

        var fresh = await client.PostAsync($"{BaseUrl}/{order.Id}/release?concurrencyToken={edited.ConcurrencyToken}", null);
        fresh.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Complete_WithStaleToken_Returns409_AndFreshTokenSucceeds()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var released = await CreateReleasedOrderAsync(client);

        var confirm = await client.PostAsJsonAsync(ConfirmationsUrl, ConfirmPayload(released.Id));
        confirm.StatusCode.Should().Be(HttpStatusCode.Created);

        var inProgress = await ReadAsync<ProductionOrderDto>(await client.GetAsync($"{BaseUrl}/{released.Id}"));
        inProgress.ConcurrencyToken.Should().NotBe(released.ConcurrencyToken);

        var stale = await client.PostAsync(
            $"{BaseUrl}/{released.Id}/complete?concurrencyToken={released.ConcurrencyToken}", null);
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await ReadAsync<JsonDocument>(stale);
        problem.RootElement.GetProperty("concurrencyToken").GetString().Should().Be(inProgress.ConcurrencyToken);

        var fresh = await client.PostAsync(
            $"{BaseUrl}/{released.Id}/complete?concurrencyToken={inProgress.ConcurrencyToken}", null);
        fresh.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Close_WithStaleToken_Returns409_AndFreshTokenSucceeds()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var released = await CreateReleasedOrderAsync(client);

        var confirm = await client.PostAsJsonAsync(ConfirmationsUrl, ConfirmPayload(released.Id));
        confirm.StatusCode.Should().Be(HttpStatusCode.Created);

        var inProgress = await ReadAsync<ProductionOrderDto>(await client.GetAsync($"{BaseUrl}/{released.Id}"));
        var complete = await client.PostAsync(
            $"{BaseUrl}/{released.Id}/complete?concurrencyToken={inProgress.ConcurrencyToken}", null);
        complete.StatusCode.Should().Be(HttpStatusCode.OK);
        var completed = await ReadAsync<ProductionOrderDto>(complete);

        var stale = await client.PostAsync(
            $"{BaseUrl}/{released.Id}/close?concurrencyToken={inProgress.ConcurrencyToken}", null);
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await ReadAsync<JsonDocument>(stale);
        problem.RootElement.GetProperty("concurrencyToken").GetString().Should().Be(completed.ConcurrencyToken);

        var fresh = await client.PostAsync(
            $"{BaseUrl}/{released.Id}/close?concurrencyToken={completed.ConcurrencyToken}", null);
        fresh.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_OrderFromAnotherTenant_Returns404_WithoutLeakingToken()
    {
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var created = await CreateOrderAsync(devClient);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);

        var response = await otherTenantClient.GetAsync($"{BaseUrl}/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("concurrencyToken");
        body.Should().NotContain(created.ConcurrencyToken);
    }

    private static object UpdatePayload(ProductionOrderDto order, string token, decimal plannedQuantity) => new
    {
        id = order.Id,
        code = order.Code,
        productId = order.ProductId,
        recipeId = order.RecipeId,
        recipeVersionId = order.RecipeVersionId,
        plannedQuantity,
        measureUnitId = (Guid?)null,
        priority = order.Priority,
        dueDate = (DateTime?)null,
        notes = (string?)null,
        syncId = (string?)null,
        concurrencyToken = token
    };

    private static object ConfirmPayload(Guid productionOrderId) => new
    {
        productionOrderId,
        machineId = Guid.NewGuid(),
        reportedByOperatorId = (Guid?)null,
        reportedAt = DateTime.UtcNow,
        goodQuantity = 10m,
        scrapQuantity = 2m,
        notes = (string?)null
    };

    private static string UniqueCode() => $"PO-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    private async Task<ProductionOrderDto> CreateOrderAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            code = UniqueCode(),
            productId = Guid.NewGuid(),
            recipeId = Guid.NewGuid(),
            recipeVersionId = Guid.NewGuid(),
            plannedQuantity = 100m,
            measureUnitId = (Guid?)null,
            priority = 0,
            dueDate = (DateTime?)null,
            notes = (string?)null,
            syncId = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<ProductionOrderDto>(response);
    }

    /// <summary>Planned order against a released recipe version (releasable, not yet released).</summary>
    private async Task<ProductionOrderDto> CreateReleasableOrderAsync(HttpClient client)
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
        operationResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var releaseVersionResponse = await client.PostAsync($"/api/recipe-versions/{versionId}/release", null);
        releaseVersionResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var orderResponse = await client.PostAsJsonAsync(BaseUrl, new
        {
            code = UniqueCode(),
            productId = Guid.NewGuid(),
            recipeId = recipe.Id,
            recipeVersionId = versionId,
            plannedQuantity = 100m,
            measureUnitId = (Guid?)null,
            priority = 0,
            dueDate = (DateTime?)null,
            notes = (string?)null,
            syncId = (string?)null
        });
        orderResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<ProductionOrderDto>(orderResponse);
    }

    private async Task<ProductionOrderDto> CreateReleasedOrderAsync(HttpClient client)
    {
        var order = await CreateReleasableOrderAsync(client);

        var releaseOrderResponse = await client.PostAsync($"{BaseUrl}/{order.Id}/release", null);
        releaseOrderResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadAsync<ProductionOrderDto>(releaseOrderResponse);
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
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<RecipeDto>(response);
    }
}
