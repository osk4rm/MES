using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for the persisted RW/PW stock ledger
/// (<c>/api/stock-movements</c>). They prove that creating a confirmation
/// posts preview-equivalent lines, that deleting a confirmation removes them
/// (FK cascade), and that the browse endpoint enforces auth, 404s and tenant
/// isolation - against a real PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class StockMovementsEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/stock-movements";
    private const string OrdersUrl = "/api/production-orders";
    private const string ConfirmationsUrl = "/api/production-confirmations";

    [Fact]
    public async Task Browse_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync($"{BaseUrl}?confirmationId={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateConfirmation_PostsPreviewEquivalentLines()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var setup = await CreateReleasedOrderWithBomAsync(client);
        var confirmation = await ConfirmAsync(client, setup.OrderId, goodQuantity: 10m);

        var previewResponse = await client.GetAsync($"{ConfirmationsUrl}/{confirmation.Id}/movements");
        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = await ReadAsync<List<MovementPreviewLineDto>>(previewResponse);

        var browseResponse = await client.GetAsync($"{BaseUrl}?confirmationId={confirmation.Id}");

        browseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var lines = await ReadAsync<List<StockMovementDto>>(browseResponse);
        lines.Should().HaveCount(preview.Count);
        foreach (var expected in preview)
        {
            lines.Should().ContainSingle(l =>
                l.MovementType == expected.MovementType
                && l.ProductId == expected.ProductId
                && l.Quantity == expected.Quantity
                && l.MeasureUnitId == expected.MeasureUnitId
                && l.WarehouseId == expected.PreferredWarehouseId);
        }
        lines.Should().OnlyContain(l =>
            l.ProductionConfirmationId == confirmation.Id && l.ProductionOrderId == setup.OrderId);
        lines.Should().ContainSingle(l => l.MovementType == "PW" && l.Quantity == 10m);
        lines.Single(l => l.ProductId == setup.PerUnitProductId).Quantity.Should().Be(20m);
        lines.Single(l => l.ProductId == setup.PerUnitProductId).WarehouseId.Should().Be(setup.PreferredWarehouseId);
        lines.Single(l => l.ProductId == setup.PerBatchProductId).Quantity.Should().Be(5.5m);
    }

    [Fact]
    public async Task ScrapOnlyConfirmation_PostsPreviewEquivalentLines()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var setup = await CreateReleasedOrderWithBomAsync(client);
        var confirmation = await ConfirmAsync(client, setup.OrderId, goodQuantity: 0m, scrapQuantity: 3m);

        var previewResponse = await client.GetAsync($"{ConfirmationsUrl}/{confirmation.Id}/movements");
        var preview = await ReadAsync<List<MovementPreviewLineDto>>(previewResponse);

        var browseResponse = await client.GetAsync($"{BaseUrl}?confirmationId={confirmation.Id}");

        browseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var lines = await ReadAsync<List<StockMovementDto>>(browseResponse);
        lines.Should().HaveCount(preview.Count);
        foreach (var expected in preview)
        {
            lines.Should().ContainSingle(l =>
                l.MovementType == expected.MovementType
                && l.ProductId == expected.ProductId
                && l.Quantity == expected.Quantity);
        }
    }

    [Fact]
    public async Task DeleteConfirmation_RemovesLines_AndRecreatePostsFreshSet()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var setup = await CreateReleasedOrderWithBomAsync(client);
        var confirmation = await ConfirmAsync(client, setup.OrderId, goodQuantity: 10m);

        var before = await client.GetAsync($"{BaseUrl}?confirmationId={confirmation.Id}");
        (await ReadAsync<List<StockMovementDto>>(before)).Should().HaveCount(3);

        var delete = await client.DeleteAsync($"{ConfirmationsUrl}/{confirmation.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await CountLinesAsync(confirmation.Id)).Should().Be(0);

        var gone = await client.GetAsync($"{BaseUrl}?confirmationId={confirmation.Id}");
        gone.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var recreated = await ConfirmAsync(client, setup.OrderId, goodQuantity: 10m);
        var after = await client.GetAsync($"{BaseUrl}?confirmationId={recreated.Id}");

        after.StatusCode.Should().Be(HttpStatusCode.OK);
        var fresh = await ReadAsync<List<StockMovementDto>>(after);
        fresh.Should().HaveCount(3);
        fresh.Should().OnlyContain(l => l.ProductionConfirmationId == recreated.Id);
    }

    [Fact]
    public async Task Browse_UnknownConfirmationId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{BaseUrl}?confirmationId={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Browse_MissingConfirmationId_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Browse_CrossTenantConfirmation_Returns404()
    {
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var setup = await CreateReleasedOrderWithBomAsync(devClient);
        var confirmation = await ConfirmAsync(devClient, setup.OrderId, goodQuantity: 2m);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);

        var response = await otherTenantClient.GetAsync($"{BaseUrl}?confirmationId={confirmation.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<int> CountLinesAsync(Guid confirmationId)
    {
        using var scope = Fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
        return await context.Set<StockMovement>()
            .IgnoreQueryFilters()
            .CountAsync(x => x.ProductionConfirmationId == confirmationId);
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
            name = "Stock ledger recipe",
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
