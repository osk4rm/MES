using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for the read-only RW/PW movement
/// previews (<c>/api/production-orders/{id}/movements</c> and
/// <c>/api/production-confirmations/{id}/movements</c>) plus the optional
/// production-order link on scrap and downtime creates. They exercise the
/// full request pipeline against a real PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ConfirmationMovementsEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string OrdersUrl = "/api/production-orders";
    private const string ConfirmationsUrl = "/api/production-confirmations";
    private const string ScrapUrl = "/api/scrap-events";
    private const string DowntimeUrl = "/api/downtime-events";

    [Fact]
    public async Task OrderMovements_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync($"{OrdersUrl}/{Guid.NewGuid()}/movements");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OrderMovements_HappyPath_ReturnsPwPlusRwLines()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var setup = await CreateReleasedOrderWithBomAsync(client);
        var confirmation = await ConfirmAsync(client, setup.OrderId, goodQuantity: 10m);

        var response = await client.GetAsync($"{OrdersUrl}/{setup.OrderId}/movements");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var lines = await ReadAsync<List<MovementPreviewLineDto>>(response);
        lines.Should().HaveCount(3);

        var pw = lines.Single(l => l.MovementType == "PW");
        pw.Quantity.Should().Be(10m);

        var rwPerUnit = lines.Single(l => l.ProductId == setup.PerUnitProductId);
        rwPerUnit.MovementType.Should().Be("RW");
        rwPerUnit.Quantity.Should().Be(20m);
        rwPerUnit.PreferredWarehouseId.Should().Be(setup.PreferredWarehouseId);

        var rwPerBatch = lines.Single(l => l.ProductId == setup.PerBatchProductId);
        rwPerBatch.Quantity.Should().Be(5.5m);
        _ = confirmation;
    }

    [Fact]
    public async Task OrderMovements_AggregatesAcrossConfirmations()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var setup = await CreateReleasedOrderWithBomAsync(client);
        await ConfirmAsync(client, setup.OrderId, goodQuantity: 10m);
        await ConfirmAsync(client, setup.OrderId, goodQuantity: 5m);

        var response = await client.GetAsync($"{OrdersUrl}/{setup.OrderId}/movements");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var lines = await ReadAsync<List<MovementPreviewLineDto>>(response);
        lines.Single(l => l.MovementType == "PW").Quantity.Should().Be(15m);
        lines.Single(l => l.ProductId == setup.PerUnitProductId).Quantity.Should().Be(30m);
    }

    [Fact]
    public async Task OrderMovements_UnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{OrdersUrl}/{Guid.NewGuid()}/movements");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OrderMovements_CrossTenantId_Returns404()
    {
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var setup = await CreateReleasedOrderWithBomAsync(devClient);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);

        var response = await otherTenantClient.GetAsync($"{OrdersUrl}/{setup.OrderId}/movements");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConfirmationMovements_HappyPath_ReturnsSplitForSingleConfirmation()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var setup = await CreateReleasedOrderWithBomAsync(client);
        var confirmation = await ConfirmAsync(client, setup.OrderId, goodQuantity: 4m);

        var response = await client.GetAsync($"{ConfirmationsUrl}/{confirmation.Id}/movements");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var lines = await ReadAsync<List<MovementPreviewLineDto>>(response);
        lines.Should().HaveCount(3);
        lines.Single(l => l.MovementType == "PW").Quantity.Should().Be(4m);
        lines.Single(l => l.ProductId == setup.PerUnitProductId).Quantity.Should().Be(8m);
    }

    [Fact]
    public async Task ConfirmationMovements_UnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{ConfirmationsUrl}/{Guid.NewGuid()}/movements");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConfirmationMovements_CrossTenantId_Returns404()
    {
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var setup = await CreateReleasedOrderWithBomAsync(devClient);
        var confirmation = await ConfirmAsync(devClient, setup.OrderId, goodQuantity: 2m);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);

        var response = await otherTenantClient.GetAsync($"{ConfirmationsUrl}/{confirmation.Id}/movements");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ScrapCreate_WithReleasedOrderLink_StoresAndReturnsLink()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var setup = await CreateReleasedOrderWithBomAsync(client);

        var createResponse = await client.PostAsJsonAsync(ScrapUrl, new
        {
            machineId = Guid.NewGuid(),
            reasonCodeId = Guid.NewGuid(),
            quantity = 3m,
            reportedAt = DateTime.UtcNow.AddMinutes(-5),
            notes = (string?)null,
            reportedByOperatorId = (Guid?)null,
            productionOrderId = (Guid?)setup.OrderId
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ScrapEventDto>(createResponse);
        created.ProductionOrderId.Should().Be(setup.OrderId);

        var getResponse = await client.GetAsync($"{ScrapUrl}/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadAsync<ScrapEventDto>(getResponse);
        fetched.ProductionOrderId.Should().Be(setup.OrderId);
    }

    [Fact]
    public async Task ScrapCreate_WithCompletedOrder_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var setup = await CreateReleasedOrderWithBomAsync(client);
        await ConfirmAsync(client, setup.OrderId, goodQuantity: 5m);
        var complete = await client.PostAsync($"{OrdersUrl}/{setup.OrderId}/complete", null);
        complete.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.PostAsJsonAsync(ScrapUrl, new
        {
            machineId = Guid.NewGuid(),
            reasonCodeId = Guid.NewGuid(),
            quantity = 1m,
            reportedAt = DateTime.UtcNow.AddMinutes(-5),
            notes = (string?)null,
            reportedByOperatorId = (Guid?)null,
            productionOrderId = (Guid?)setup.OrderId
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ScrapCreate_WithCrossTenantOrder_Returns404()
    {
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var setup = await CreateReleasedOrderWithBomAsync(devClient);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);

        var response = await otherTenantClient.PostAsJsonAsync(ScrapUrl, new
        {
            machineId = Guid.NewGuid(),
            reasonCodeId = Guid.NewGuid(),
            quantity = 1m,
            reportedAt = DateTime.UtcNow.AddMinutes(-5),
            notes = (string?)null,
            reportedByOperatorId = (Guid?)null,
            productionOrderId = (Guid?)setup.OrderId
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DowntimeStart_WithReleasedOrderLink_StoresAndReturnsLink()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var setup = await CreateReleasedOrderWithBomAsync(client);

        var startResponse = await client.PostAsJsonAsync(DowntimeUrl, new
        {
            machineId = Guid.NewGuid(),
            reasonCodeId = Guid.NewGuid(),
            startedAt = DateTime.UtcNow.AddMinutes(-30),
            notes = (string?)null,
            reportedByOperatorId = (Guid?)null,
            productionOrderId = (Guid?)setup.OrderId
        });

        startResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<DowntimeEventDto>(startResponse);
        created.ProductionOrderId.Should().Be(setup.OrderId);
    }

    [Fact]
    public async Task DowntimeStart_WithCompletedOrder_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var setup = await CreateReleasedOrderWithBomAsync(client);
        await ConfirmAsync(client, setup.OrderId, goodQuantity: 5m);
        var complete = await client.PostAsync($"{OrdersUrl}/{setup.OrderId}/complete", null);
        complete.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.PostAsJsonAsync(DowntimeUrl, new
        {
            machineId = Guid.NewGuid(),
            reasonCodeId = Guid.NewGuid(),
            startedAt = DateTime.UtcNow.AddMinutes(-30),
            notes = (string?)null,
            reportedByOperatorId = (Guid?)null,
            productionOrderId = (Guid?)setup.OrderId
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
    /// of 5 units with 10% scrap), releases the version, creates an order
    /// against it and releases the order.
    /// </summary>
    private async Task<OrderSetup> CreateReleasedOrderWithBomAsync(HttpClient client)
    {
        var recipeResponse = await client.PostAsJsonAsync("/api/recipes", new
        {
            code = UniqueCode("R"),
            name = "Movement preview recipe",
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
        var perBatchProductId = Guid.NewGuid();
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
            productId = perBatchProductId,
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
        var order = await ReadAsync<ProductionOrderDto>(orderResponse);

        var releaseOrder = await client.PostAsync($"{OrdersUrl}/{order.Id}/release", null);
        releaseOrder.StatusCode.Should().Be(HttpStatusCode.OK);
        var released = await ReadAsync<ProductionOrderDto>(releaseOrder);

        return new OrderSetup(released.Id, perUnitProductId, perBatchProductId, preferredWarehouseId);
    }

    private async Task<ProductionConfirmationDto> ConfirmAsync(
        HttpClient client, Guid orderId, decimal goodQuantity)
    {
        var create = await client.PostAsJsonAsync(ConfirmationsUrl, new
        {
            productionOrderId = orderId,
            machineId = Guid.NewGuid(),
            reportedByOperatorId = (Guid?)null,
            reportedAt = DateTime.UtcNow,
            goodQuantity,
            scrapQuantity = 0m,
            notes = (string?)null
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<ProductionConfirmationDto>(create);
    }
}
