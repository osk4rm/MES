using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for the atomic confirmation fan-out
/// (issue #265): one POST persists the confirmation row, all RW/PW movements,
/// all genealogy edges and the order status flip together, while any failure
/// leaves zero partial rows behind.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ProductionConfirmationAtomicityEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/production-confirmations";
    private const string OrdersUrl = "/api/production-orders";
    private const string StockMovementsUrl = "/api/stock-movements";
    private const string GenealogyUrl = "/api/lot-genealogy";
    private const string LotsUrl = "/api/lots";

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_HappyPath_PersistsConfirmationMovementsEdgesAndOrderFlipTogether()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var setup = await CreateReleasedOrderWithBomAsync(client);
        var produced = await CreateLotAsync(client);
        var consumed = await CreateLotAsync(client);

        // Act
        var createResponse = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(
            setup.OrderId, producedLotId: produced.Id,
            consumedLots: new (Guid LotId, decimal Quantity)[] { (consumed.Id, 5m) }));

        // Assert
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ProductionConfirmationDto>(createResponse);

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var previewResponse = await client.GetAsync($"{BaseUrl}/{created.Id}/movements");
        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = await ReadAsync<List<MovementPreviewLineDto>>(previewResponse);

        var linesResponse = await client.GetAsync($"{StockMovementsUrl}?confirmationId={created.Id}");
        linesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var lines = await ReadAsync<List<StockMovementDto>>(linesResponse);
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

        var edgesResponse = await client.GetAsync($"{GenealogyUrl}?productionConfirmationId={created.Id}");
        edgesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var edges = await ReadAsync<PagedResponseDto<LotGenealogyEdgeDto>>(edgesResponse);
        edges.Items.Should().ContainSingle(i =>
            i.ConsumedLotId == consumed.Id
            && i.ProducedLotId == produced.Id
            && i.ProductionOrderId == setup.OrderId
            && i.ProductionConfirmationId == created.Id);

        var orderResponse = await client.GetAsync($"{OrdersUrl}/{setup.OrderId}");
        var fetchedOrder = await ReadAsync<ProductionOrderDto>(orderResponse);
        fetchedOrder.Status.Should().Be((short)ProductionOrderStatus.InProgress);
    }

    [Fact]
    public async Task Create_UnknownConsumedLot_Returns404_AndWritesNothing()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var setup = await CreateReleasedOrderWithBomAsync(client);
        var produced = await CreateLotAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(
            setup.OrderId, producedLotId: produced.Id,
            consumedLots: new (Guid LotId, decimal Quantity)[] { (Guid.NewGuid(), 5m) }));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CountConfirmationsAsync(setup.OrderId)).Should().Be(0);
        (await CountMovementsForOrderAsync(setup.OrderId)).Should().Be(0);
        (await CountEdgesForOrderAsync(setup.OrderId)).Should().Be(0);

        var orderResponse = await client.GetAsync($"{OrdersUrl}/{setup.OrderId}");
        var fetchedOrder = await ReadAsync<ProductionOrderDto>(orderResponse);
        fetchedOrder.Status.Should().Be((short)ProductionOrderStatus.Released);
    }

    [Fact]
    public async Task Create_BadQuantity_Returns400_AndWritesNothing()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var setup = await CreateReleasedOrderWithBomAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(
            BaseUrl, ConfirmPayload(setup.OrderId, goodQuantity: 0m, scrapQuantity: 0m));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CountConfirmationsAsync(setup.OrderId)).Should().Be(0);
        (await CountMovementsForOrderAsync(setup.OrderId)).Should().Be(0);
        (await CountEdgesForOrderAsync(setup.OrderId)).Should().Be(0);
    }

    [Fact]
    public async Task Create_CrossTenantOrder_Returns404_AndWritesNothing()
    {
        // Arrange
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var setup = await CreateReleasedOrderWithBomAsync(devClient);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);

        // Act
        var response = await otherTenantClient.PostAsJsonAsync(BaseUrl, ConfirmPayload(setup.OrderId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var browse = await otherTenantClient.GetAsync($"{BaseUrl}?productionOrderId={setup.OrderId}");
        browse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<ProductionConfirmationDto>>(browse);
        page.Items.Should().BeEmpty();

        (await CountConfirmationsAsync(setup.OrderId)).Should().Be(0);
        (await CountMovementsForOrderAsync(setup.OrderId)).Should().Be(0);
        (await CountEdgesForOrderAsync(setup.OrderId)).Should().Be(0);
    }

    [Fact]
    public async Task Create_UnknownOrderId_Returns404_AndWritesNothing()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var unknownOrderId = Guid.NewGuid();

        // Act
        var response = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(unknownOrderId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CountConfirmationsAsync(unknownOrderId)).Should().Be(0);
        (await CountMovementsForOrderAsync(unknownOrderId)).Should().Be(0);
        (await CountEdgesForOrderAsync(unknownOrderId)).Should().Be(0);
    }

    [Fact]
    public async Task Create_ClosedOrder_Returns409_AndWritesNothing()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var setup = await CreateReleasedOrderWithBomAsync(client);
        await SetOrderStatusAsync(setup.OrderId, ProductionOrderStatus.Closed);

        // Act
        var response = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(setup.OrderId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CountConfirmationsAsync(setup.OrderId)).Should().Be(0);
        (await CountMovementsForOrderAsync(setup.OrderId)).Should().Be(0);
        (await CountEdgesForOrderAsync(setup.OrderId)).Should().Be(0);
    }

    private async Task<int> CountConfirmationsAsync(Guid orderId)
    {
        using var scope = Fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
        return await context.Set<ProductionConfirmation>()
            .IgnoreQueryFilters()
            .CountAsync(x => x.ProductionOrderId == orderId);
    }

    private async Task<int> CountMovementsForOrderAsync(Guid orderId)
    {
        using var scope = Fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
        return await context.Set<StockMovement>()
            .IgnoreQueryFilters()
            .CountAsync(x => x.ProductionOrderId == orderId);
    }

    private async Task<int> CountEdgesForOrderAsync(Guid orderId)
    {
        using var scope = Fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
        return await context.Set<LotGenealogyEdge>()
            .IgnoreQueryFilters()
            .CountAsync(x => x.ProductionOrderId == orderId);
    }

    private async Task SetOrderStatusAsync(Guid orderId, ProductionOrderStatus status)
    {
        using var scope = Fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
        var order = await context.Set<ProductionOrder>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == orderId);
        order.Should().NotBeNull();
        order!.Status = status;
        await context.SaveChangesAsync();
    }

    private static object ConfirmPayload(
        Guid productionOrderId,
        Guid? machineId = null,
        decimal goodQuantity = 10m,
        decimal scrapQuantity = 2m,
        DateTime? reportedAt = null,
        Guid? producedLotId = null,
        (Guid LotId, decimal Quantity)[]? consumedLots = null) => new
        {
            productionOrderId,
            machineId = machineId ?? Guid.NewGuid(),
            reportedByOperatorId = (Guid?)null,
            reportedAt = reportedAt ?? DateTime.UtcNow,
            goodQuantity,
            scrapQuantity,
            notes = (string?)null,
            producedLotId,
            consumedLots = consumedLots?.Select(e => new { lotId = e.LotId, quantity = e.Quantity }).ToArray()
        };

    private static string UniqueCode(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    private static async Task<LotDto> CreateLotAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(LotsUrl, new
        {
            id = Guid.Empty,
            code = UniqueCode("LOT"),
            productId = Guid.NewGuid(),
            measureUnitId = Guid.NewGuid(),
            quantity = 100m,
            supplierLotNumber = (string?)null,
            producedAt = (DateTime?)null,
            expiryDate = (DateTime?)null,
            notes = (string?)null
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<LotDto>(response);
    }

    private sealed record OrderSetup(
        Guid OrderId,
        Guid PerUnitProductId,
        Guid PerBatchProductId,
        Guid PreferredWarehouseId);

    private sealed record OperationNodeDto(Guid Id);

    private async Task<OrderSetup> CreateReleasedOrderWithBomAsync(HttpClient client)
    {
        var recipeResponse = await client.PostAsJsonAsync("/api/recipes", new
        {
            code = UniqueCode("R"),
            name = "Atomic fan-out recipe",
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

        var perUnitProductId = Guid.NewGuid();
        var perBatchProductId = Guid.NewGuid();
        var preferredWarehouseId = Guid.NewGuid();

        var bomPerUnit = await client.PostAsJsonAsync($"/api/operations/{operation.Id}/bom-items", new
        {
            operationId = operation.Id,
            productId = perUnitProductId,
            measureUnitId = (Guid?)null,
            quantity = 2m,
            quantityType = 1,
            scrapPercentage = (decimal?)null,
            isOptional = false,
            preferredWarehouseId = (Guid?)preferredWarehouseId,
            consumptionTiming = 1,
            notes = (string?)null,
            sortIndex = (int?)0
        });
        bomPerUnit.StatusCode.Should().Be(HttpStatusCode.OK);

        var bomPerBatch = await client.PostAsJsonAsync($"/api/operations/{operation.Id}/bom-items", new
        {
            operationId = operation.Id,
            productId = perBatchProductId,
            measureUnitId = (Guid?)null,
            quantity = 5m,
            quantityType = 2,
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
}
