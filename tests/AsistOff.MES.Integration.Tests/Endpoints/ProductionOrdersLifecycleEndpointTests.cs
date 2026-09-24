using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Lifecycle integration tests for completing and closing Production Orders
/// (<c>/api/production-orders/{id}/complete</c> and <c>.../close</c>).
/// They exercise the full request pipeline against a real PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ProductionOrdersLifecycleEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/production-orders";
    private const string ConfirmationsUrl = "/api/production-confirmations";

    [Fact]
    public async Task Complete_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.PostAsync($"{BaseUrl}/{Guid.NewGuid()}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Close_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.PostAsync($"{BaseUrl}/{Guid.NewGuid()}/close", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FullFlow_ReleaseConfirmCompleteClose_TransitionsWithTotals()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);

        // Released order has zero totals.
        var fetched = await ReadAsync<ProductionOrderDto>(await client.GetAsync($"{BaseUrl}/{order.Id}"));
        fetched.Status.Should().Be(2); // Released
        fetched.ProducedQuantity.Should().Be(0m);
        fetched.ConfirmationsCount.Should().Be(0);

        // First confirmation moves the order to InProgress.
        var confirm = await client.PostAsJsonAsync(ConfirmationsUrl, ConfirmPayload(order.Id, goodQuantity: 60m, scrapQuantity: 5m));
        confirm.StatusCode.Should().Be(HttpStatusCode.Created);

        var inProgress = await ReadAsync<ProductionOrderDto>(await client.GetAsync($"{BaseUrl}/{order.Id}"));
        inProgress.Status.Should().Be(3); // InProgress
        inProgress.ProducedQuantity.Should().Be(60m);
        inProgress.ScrappedQuantity.Should().Be(5m);
        inProgress.RemainingQuantity.Should().Be(40m);
        inProgress.ConfirmationsCount.Should().Be(1);

        // Browse reflects the same totals.
        var browse = await client.GetAsync($"{BaseUrl}?code={order.Code}");
        browse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<ProductionOrderDto>>(browse);
        var browsed = page.Items.Single(x => x.Id == order.Id);
        browsed.ProducedQuantity.Should().Be(60m);
        browsed.RemainingQuantity.Should().Be(40m);
        browsed.ConfirmationsCount.Should().Be(1);

        // Complete moves InProgress -> Completed with timestamps.
        var complete = await client.PostAsync($"{BaseUrl}/{order.Id}/complete", null);
        complete.StatusCode.Should().Be(HttpStatusCode.OK);
        var completed = await ReadAsync<ProductionOrderDto>(complete);
        completed.Status.Should().Be(4); // Completed
        completed.ProducedQuantity.Should().Be(60m);
        completed.CompletedAt.Should().NotBeNull();
        completed.ClosedAt.Should().BeNull();

        // Confirming a Completed order is rejected.
        var confirmAfterComplete = await client.PostAsJsonAsync(ConfirmationsUrl, ConfirmPayload(order.Id));
        confirmAfterComplete.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Close moves Completed -> Closed with timestamps.
        var close = await client.PostAsync($"{BaseUrl}/{order.Id}/close", null);
        close.StatusCode.Should().Be(HttpStatusCode.OK);
        var closed = await ReadAsync<ProductionOrderDto>(close);
        closed.Status.Should().Be(5); // Closed
        closed.ClosedAt.Should().NotBeNull();
        closed.CompletedAt.Should().NotBeNull();

        // Confirming a Closed order is rejected.
        var confirmAfterClose = await client.PostAsJsonAsync(ConfirmationsUrl, ConfirmPayload(order.Id));
        confirmAfterClose.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Complete_WithoutGoodQuantity_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);
        var scrapOnly = await client.PostAsJsonAsync(
            ConfirmationsUrl, ConfirmPayload(order.Id, goodQuantity: 0m, scrapQuantity: 3m));
        scrapOnly.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.PostAsync($"{BaseUrl}/{order.Id}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Complete_WithoutAnyConfirmation_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);
        var confirm = await client.PostAsJsonAsync(ConfirmationsUrl, ConfirmPayload(order.Id));
        confirm.StatusCode.Should().Be(HttpStatusCode.Created);

        // Delete the only confirmation so the order is InProgress with no good quantity.
        var created = await ReadAsync<ProductionConfirmationDto>(confirm);
        var deleted = await client.DeleteAsync($"{ConfirmationsUrl}/{created.Id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Order stays InProgress (status never rolls back) but has no confirmations.
        var response = await client.PostAsync($"{BaseUrl}/{order.Id}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("close")]
    public async Task CompleteClose_PlannedOrder_Returns409(string action)
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateOrderAsync(client);

        var response = await client.PostAsync($"{BaseUrl}/{order.Id}/{action}", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Complete_AlreadyCompleted_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateCompletedOrderAsync(client);

        var response = await client.PostAsync($"{BaseUrl}/{order.Id}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Complete_ClosedOrder_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateCompletedOrderAsync(client);
        var close = await client.PostAsync($"{BaseUrl}/{order.Id}/close", null);
        close.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.PostAsync($"{BaseUrl}/{order.Id}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Close_InProgressOrder_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);
        var confirm = await client.PostAsJsonAsync(ConfirmationsUrl, ConfirmPayload(order.Id));
        confirm.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.PostAsync($"{BaseUrl}/{order.Id}/close", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Close_AlreadyClosed_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateCompletedOrderAsync(client);
        var first = await client.PostAsync($"{BaseUrl}/{order.Id}/close", null);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PostAsync($"{BaseUrl}/{order.Id}/close", null);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Complete_UnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsync($"{BaseUrl}/{Guid.NewGuid()}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Close_UnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsync($"{BaseUrl}/{Guid.NewGuid()}/close", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Complete_CrossTenantOrder_Returns404()
    {
        using var owner = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateCompletedOrderInProgressAsync(owner);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenant = await Fixture.CreateAuthenticatedClientAsync(email, password);

        var response = await otherTenant.PostAsync($"{BaseUrl}/{order.Id}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Close_CrossTenantOrder_Returns404()
    {
        using var owner = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateCompletedOrderAsync(owner);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenant = await Fixture.CreateAuthenticatedClientAsync(email, password);

        var response = await otherTenant.PostAsync($"{BaseUrl}/{order.Id}/close", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static object ConfirmPayload(
        Guid productionOrderId,
        decimal goodQuantity = 10m,
        decimal scrapQuantity = 2m) => new
        {
            productionOrderId,
            machineId = Guid.NewGuid(),
            reportedByOperatorId = (Guid?)null,
            reportedAt = DateTime.UtcNow,
            goodQuantity,
            scrapQuantity,
            notes = (string?)null
        };

    private static string UniqueCode() => $"PO-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    private async Task<ProductionOrderDto> CreateOrderAsync(HttpClient client)
    {
        var payload = new
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
        };

        var response = await client.PostAsJsonAsync(BaseUrl, payload);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<ProductionOrderDto>(response);
    }

    private async Task<ProductionOrderDto> CreateReleasedOrderAsync(HttpClient client)
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

        var payload = new
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
        };

        var orderResponse = await client.PostAsJsonAsync(BaseUrl, payload);
        orderResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await ReadAsync<ProductionOrderDto>(orderResponse);

        var releaseOrderResponse = await client.PostAsync($"{BaseUrl}/{order.Id}/release", null);
        releaseOrderResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadAsync<ProductionOrderDto>(releaseOrderResponse);
    }

    /// <summary>Released order with one good confirmation (InProgress).</summary>
    private async Task<ProductionOrderDto> CreateCompletedOrderInProgressAsync(HttpClient client)
    {
        var order = await CreateReleasedOrderAsync(client);
        var confirm = await client.PostAsJsonAsync(ConfirmationsUrl, ConfirmPayload(order.Id));
        confirm.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<ProductionOrderDto>(await client.GetAsync($"{BaseUrl}/{order.Id}"));
    }

    /// <summary>Order moved to Completed via the API.</summary>
    private async Task<ProductionOrderDto> CreateCompletedOrderAsync(HttpClient client)
    {
        var order = await CreateReleasedOrderAsync(client);
        var confirm = await client.PostAsJsonAsync(ConfirmationsUrl, ConfirmPayload(order.Id));
        confirm.StatusCode.Should().Be(HttpStatusCode.Created);
        var complete = await client.PostAsync($"{BaseUrl}/{order.Id}/complete", null);
        complete.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadAsync<ProductionOrderDto>(complete);
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
