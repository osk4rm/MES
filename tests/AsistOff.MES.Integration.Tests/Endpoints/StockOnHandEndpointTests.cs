using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for the read-only stock-on-hand
/// aggregation (<c>/api/stock-on-hand</c>). They prove that seeding
/// confirmations over HTTP surfaces signed balances (PW receipts add, RW
/// issues subtract) grouped by product and warehouse, that the optional
/// filters narrow the result set, and that auth and tenant isolation hold -
/// against a real PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class StockOnHandEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/stock-on-hand";
    private const string OrdersUrl = "/api/production-orders";
    private const string ConfirmationsUrl = "/api/production-confirmations";

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_AfterConfirmation_ReportsSignedBalanceForSharedPair()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        // The order product doubles as the per-batch BOM product, so the PW
        // receipt (10 pcs, unassigned warehouse) and the per-batch RW issue
        // (5 pcs plus 10% scrap = 5.5 pcs, unassigned warehouse) land on the
        // same product and warehouse pair: 10 - 5.5 = 4.5 pcs.
        var finishedProductId = Guid.NewGuid();
        var setup = await CreateReleasedOrderWithBomAsync(client, finishedProductId);
        await ConfirmAsync(client, setup.OrderId, goodQuantity: 10m);

        var response = await client.GetAsync($"{BaseUrl}?productId={finishedProductId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var balances = await ReadAsync<List<StockOnHandDto>>(response);
        balances.Should().ContainSingle()
            .Which.Should().Be(new StockOnHandDto(finishedProductId, null, 4.5m, 0m, 4.5m));

        // The per-unit RW issue (2 pcs per unit over 10 pcs = 20 pcs) sits on
        // its own pair with no receipt, hence a negative balance.
        var warehouseResponse = await client.GetAsync($"{BaseUrl}?warehouseId={setup.PreferredWarehouseId}");

        warehouseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var warehouseBalances = await ReadAsync<List<StockOnHandDto>>(warehouseResponse);
        warehouseBalances.Should().ContainSingle()
            .Which.Should().Be(new StockOnHandDto(setup.PerUnitProductId, setup.PreferredWarehouseId, -20m, 180m, -200m));
    }

    [Fact]
    public async Task Get_WithoutFilters_ReturnsAllBalances()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var finishedProductId = Guid.NewGuid();
        var setup = await CreateReleasedOrderWithBomAsync(client, finishedProductId);
        await ConfirmAsync(client, setup.OrderId, goodQuantity: 10m);

        var response = await client.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var balances = await ReadAsync<List<StockOnHandDto>>(response);
        balances.Should().ContainSingle(b =>
            b.ProductId == finishedProductId && b.WarehouseId == null && b.QuantityOnHand == 4.5m
            && b.ReservedQuantity == 0m && b.AvailableQuantity == 4.5m);
        balances.Should().ContainSingle(b =>
            b.ProductId == setup.PerUnitProductId
            && b.WarehouseId == setup.PreferredWarehouseId
            && b.QuantityOnHand == -20m
            && b.ReservedQuantity == 180m
            && b.AvailableQuantity == -200m);
    }

    [Fact]
    public async Task Get_EmptyProductId_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{BaseUrl}?productId={Guid.Empty}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_CrossTenant_DoesNotLeakBalances()
    {
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var finishedProductId = Guid.NewGuid();
        var setup = await CreateReleasedOrderWithBomAsync(devClient, finishedProductId);
        await ConfirmAsync(devClient, setup.OrderId, goodQuantity: 10m);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);

        var filtered = await otherTenantClient.GetAsync($"{BaseUrl}?productId={finishedProductId}");
        filtered.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<List<StockOnHandDto>>(filtered)).Should().BeEmpty();

        var all = await otherTenantClient.GetAsync(BaseUrl);
        all.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<List<StockOnHandDto>>(all))
            .Should().NotContain(b => b.ProductId == finishedProductId);
    }

    private sealed record OrderSetup(
        Guid OrderId,
        Guid PerUnitProductId,
        Guid PerBatchProductId,
        Guid PreferredWarehouseId);

    private sealed record OperationNodeDto(Guid Id);

    private static string UniqueCode(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    /// <summary>
    /// Builds a real recipe with one operation and two BOM items (a PerUnit
    /// item of 2 units with a preferred warehouse hint, and a PerBatch item
    /// of 5 units with 10% scrap and no warehouse hint), releases the
    /// version, creates an order for <paramref name="orderProductId"/> against
    /// it and releases the order.
    /// </summary>
    private async Task<OrderSetup> CreateReleasedOrderWithBomAsync(HttpClient client, Guid orderProductId)
    {
        var recipeResponse = await client.PostAsJsonAsync("/api/recipes", new
        {
            code = UniqueCode("R"),
            name = "Stock on hand recipe",
            description = (string?)null,
            isActive = true,
            primaryProductId = (Guid?)null,
            syncId = (string?)null
        });
        recipeResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var recipe = await ReadAsync<RecipeDto>(recipeResponse);
        var versionId = recipe.Versions.Single().Id;

        var operationResponse = await client.PostAsJsonAsync("/api/operations", new
        {
            versionId,
            code = UniqueCode("OP"),
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
        var operation = await ReadAsync<OperationNodeDto>(operationResponse);
        var operationId = operation.Id;

        var perUnitProductId = Guid.NewGuid();
        var preferredWarehouseId = Guid.NewGuid();

        var bomPerUnit = await client.PostAsJsonAsync($"/api/operations/{operationId}/bom-items", new
        {
            operationId,
            productId = perUnitProductId,
            measureUnitId = (Guid?)null,
            quantity = 2m,
            quantityType = 1, // PerUnit
            scrapPercentage = (decimal?)null,
            isOptional = false,
            preferredWarehouseId = (Guid?)preferredWarehouseId,
            consumptionTiming = 1,
            notes = (string?)null,
            sortIndex = (int?)0
        });
        bomPerUnit.StatusCode.Should().Be(HttpStatusCode.OK);

        var bomPerBatch = await client.PostAsJsonAsync($"/api/operations/{operationId}/bom-items", new
        {
            operationId,
            productId = orderProductId,
            measureUnitId = (Guid?)null,
            quantity = 5m,
            quantityType = 2, // PerBatch
            scrapPercentage = (decimal?)10m,
            isOptional = false,
            preferredWarehouseId = (Guid?)null,
            consumptionTiming = 1,
            notes = (string?)null,
            sortIndex = (int?)1
        });
        bomPerBatch.StatusCode.Should().Be(HttpStatusCode.OK);

        var releaseVersion = await client.PostAsync($"/api/recipe-versions/{versionId}/release", null);
        releaseVersion.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var orderResponse = await client.PostAsJsonAsync(OrdersUrl, new
        {
            code = UniqueCode("PO"),
            productId = orderProductId,
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
        var order = await ReadAsync<ProductionOrderDto>(orderResponse);

        var releaseOrder = await client.PostAsync($"{OrdersUrl}/{order.Id}/release", null);
        releaseOrder.StatusCode.Should().Be(HttpStatusCode.OK);
        var released = await ReadAsync<ProductionOrderDto>(releaseOrder);

        return new OrderSetup(released.Id, perUnitProductId, orderProductId, preferredWarehouseId);
    }

    private async Task<ProductionConfirmationDto> ConfirmAsync(
        HttpClient client, Guid orderId, decimal goodQuantity, decimal scrapQuantity = 0m)
    {
        var create = await client.PostAsJsonAsync(ConfirmationsUrl, new
        {
            productionOrderId = orderId,
            machineId = Guid.NewGuid(),
            reportedByOperatorId = (Guid?)null,
            reportedAt = DateTime.UtcNow,
            goodQuantity,
            scrapQuantity,
            notes = (string?)null
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<ProductionConfirmationDto>(create);
    }
}
