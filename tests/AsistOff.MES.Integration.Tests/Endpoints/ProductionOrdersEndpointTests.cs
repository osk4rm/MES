using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>/api/production-orders</c>. They exercise
/// the full request pipeline: authentication, tenant resolution, MediatR handlers,
/// EF Core persistence and the global exception handler - against a real PostgreSQL
/// database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ProductionOrdersEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/production-orders";

    [Fact]
    public async Task Browse_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_ReturnsCreated_AndIsRetrievableById()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();
        var payload = new
        {
            code,
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

        var createResponse = await client.PostAsJsonAsync(BaseUrl, payload);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ProductionOrderDto>(createResponse);
        created.Code.Should().Be(code);
        created.Status.Should().Be(1); // Planned
        created.Id.Should().NotBeEmpty();

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadAsync<ProductionOrderDto>(getResponse);
        fetched.Id.Should().Be(created.Id);
        fetched.RecipeVersionId.Should().Be(payload.recipeVersionId);
    }

    [Fact]
    public async Task Create_WithDuplicateCode_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();
        var payload = new
        {
            code,
            productId = Guid.NewGuid(),
            recipeId = Guid.NewGuid(),
            recipeVersionId = Guid.NewGuid(),
            plannedQuantity = 50m,
            measureUnitId = (Guid?)null,
            priority = 0,
            dueDate = (DateTime?)null,
            notes = (string?)null,
            syncId = (string?)null
        };

        var first = await client.PostAsJsonAsync(BaseUrl, payload);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync(BaseUrl, payload);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_WithEmptyCode_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var payload = new
        {
            code = "",
            productId = Guid.NewGuid(),
            recipeId = Guid.NewGuid(),
            recipeVersionId = Guid.NewGuid(),
            plannedQuantity = 50m,
            measureUnitId = (Guid?)null,
            priority = 0,
            dueDate = (DateTime?)null,
            notes = (string?)null,
            syncId = (string?)null
        };

        var response = await client.PostAsJsonAsync(BaseUrl, payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_PlannedOrder_Returns200()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var created = await CreateOrderAsync(client);
        var payload = new
        {
            id = created.Id,
            code = created.Code,
            productId = created.ProductId,
            recipeId = created.RecipeId,
            recipeVersionId = created.RecipeVersionId,
            plannedQuantity = 777m,
            measureUnitId = (Guid?)null,
            priority = 5,
            dueDate = (DateTime?)null,
            notes = "updated",
            syncId = (string?)null
        };

        var response = await client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await ReadAsync<ProductionOrderDto>(response);
        updated.PlannedQuantity.Should().Be(777m);
        updated.Priority.Should().Be(5);
    }

    [Fact]
    public async Task Update_ReleasedOrder_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var released = await CreateReleasedOrderAsync(client);
        var payload = new
        {
            id = released.Id,
            code = released.Code,
            productId = released.ProductId,
            recipeId = released.RecipeId,
            recipeVersionId = released.RecipeVersionId,
            plannedQuantity = 999m,
            measureUnitId = (Guid?)null,
            priority = 0,
            dueDate = (DateTime?)null,
            notes = (string?)null,
            syncId = (string?)null
        };

        var response = await client.PutAsJsonAsync($"{BaseUrl}/{released.Id}", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_PlannedOrder_Returns204()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var created = await CreateOrderAsync(client);

        var deleteResponse = await client.DeleteAsync($"{BaseUrl}/{created.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Release_PlannedOrderWithReleasedVersion_Returns200WithReleasedStatus()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var created = await CreateReleasedOrderAsync(client, release: false);

        var response = await client.PostAsync($"{BaseUrl}/{created.Id}/release", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var released = await ReadAsync<ProductionOrderDto>(response);
        released.Status.Should().Be(2); // Released
        released.ReleasedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Release_SecondTime_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var released = await CreateReleasedOrderAsync(client);

        var response = await client.PostAsync($"{BaseUrl}/{released.Id}/release", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Release_WithDraftRecipeVersion_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        // Recipe created via API always starts with a Draft version - never released here.
        var recipe = await CreateRecipeAsync(client);
        var draftVersionId = recipe.Versions.Single().Id;
        var order = await CreateOrderAsync(client, recipe.Id, draftVersionId);

        var response = await client.PostAsync($"{BaseUrl}/{order.Id}/release", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_OrderFromAnotherTenant_Returns404()
    {
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var created = await CreateOrderAsync(devClient);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);

        var response = await otherTenantClient.GetAsync($"{BaseUrl}/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static string UniqueCode() => $"PO-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    private async Task<ProductionOrderDto> CreateOrderAsync(
        HttpClient client, Guid? recipeId = null, Guid? recipeVersionId = null)
    {
        var payload = new
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
        };

        var response = await client.PostAsJsonAsync(BaseUrl, payload);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<ProductionOrderDto>(response);
    }

    /// <summary>
    /// Builds a real recipe with one operation, releases its version, creates an
    /// order against it and optionally releases the order.
    /// </summary>
    private async Task<ProductionOrderDto> CreateReleasedOrderAsync(HttpClient client, bool release = true)
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

        var order = await CreateOrderAsync(client, recipe.Id, versionId);

        if (!release)
            return order;

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
