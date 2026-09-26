using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for atomic operator confirmation
/// fan-out (issue #265). They prove that posting a confirmation persists
/// the confirmation row, all RW/PW movements, all genealogy edges, and the
/// order status flip together, and that every validation failure
/// (404/400/409) leaves zero partial rows behind. Mid-fan-out persistence
/// failures (movement/edge insert throwing) are proven to roll back the
/// staged confirmation by the handler unit tests with a failing repository
/// plus the real <c>ProductionUnitOfWork</c> database transaction.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ProductionConfirmationAtomicityEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/production-confirmations";
    private const string OrdersUrl = "/api/production-orders";
    private const string LotsUrl = "/api/lots";
    private const string GenealogyUrl = "/api/lot-genealogy";
    private const string StockMovementsUrl = "/api/stock-movements";

    [Fact]
    public async Task Create_HappyPath_PersistsConfirmationMovementsEdgesAndOrderFlipTogether()
    {
        // Arrange
        var (email, password) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var order = await CreateReleasedOrderAsync(client);
        var produced = await CreateLotAsync(client);
        var consumed = await CreateLotAsync(client);
        var machineId = Guid.NewGuid();

        // Act
        var createResponse = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(
            order.Id, machineId: machineId, producedLotId: produced.Id,
            consumedLots: new (Guid LotId, decimal Quantity)[] { (consumed.Id, 5m) }));

        // Assert
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ProductionConfirmationDto>(createResponse);

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var previewResponse = await client.GetAsync($"{BaseUrl}/{created.Id}/movements");
        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = await ReadAsync<List<MovementPreviewLineDto>>(previewResponse);

        var movementsResponse = await client.GetAsync($"{StockMovementsUrl}?confirmationId={created.Id}");
        movementsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var movements = await ReadAsync<List<StockMovementDto>>(movementsResponse);
        movements.Should().HaveCount(preview.Count);
        foreach (var expected in preview)
        {
            movements.Should().ContainSingle(l =>
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
            && i.ProductionOrderId == order.Id
            && i.ProductionConfirmationId == created.Id
            && i.MachineId == machineId);

        var orderResponse = await client.GetAsync($"{OrdersUrl}/{order.Id}");
        var fetchedOrder = await ReadAsync<ProductionOrderDto>(orderResponse);
        fetchedOrder.Status.Should().Be((short)ProductionOrderStatus.InProgress);
    }

    [Fact]
    public async Task Create_UnknownLot_Returns404_AndWritesNothing()
    {
        // Arrange
        var (email, password) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var order = await CreateReleasedOrderAsync(client);
        var consumed = await CreateLotAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(
            order.Id, producedLotId: Guid.NewGuid(),
            consumedLots: new (Guid LotId, decimal Quantity)[] { (consumed.Id, 5m) }));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CountConfirmationsForOrderAsync(order.Id)).Should().Be(0);
        (await BrowseForOrderAsync(client, order.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Create_BadQuantity_Returns400_AndWritesNothing()
    {
        // Arrange
        var (email, password) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var order = await CreateReleasedOrderAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(
            BaseUrl, ConfirmPayload(order.Id, goodQuantity: 0m, scrapQuantity: 0m));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CountConfirmationsForOrderAsync(order.Id)).Should().Be(0);
        (await BrowseForOrderAsync(client, order.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Create_ClosedOrder_Returns409_AndWritesNothing()
    {
        // Arrange
        var (email, password) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var order = await CreateReleasedOrderAsync(client);
        await SetOrderStatusAsync(order.Id, ProductionOrderStatus.Closed);

        // Act
        var response = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(order.Id));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CountConfirmationsForOrderAsync(order.Id)).Should().Be(0);
        (await BrowseForOrderAsync(client, order.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Create_CrossTenantOrder_Returns404_AndWritesNothing()
    {
        // Arrange
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(devClient);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);

        // Act
        var response = await otherTenantClient.PostAsJsonAsync(BaseUrl, ConfirmPayload(order.Id));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CountConfirmationsForOrderAsync(order.Id)).Should().Be(0);
    }

    private async Task<int> CountConfirmationsForOrderAsync(Guid orderId)
    {
        using var scope = Fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
        return await context.Set<ProductionConfirmation>()
            .IgnoreQueryFilters()
            .CountAsync(x => x.ProductionOrderId == orderId);
    }

    private async Task<IReadOnlyCollection<ProductionConfirmationDto>> BrowseForOrderAsync(
        HttpClient client, Guid orderId)
    {
        var response = await client.GetAsync($"{BaseUrl}?productionOrderId={orderId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<ProductionConfirmationDto>>(response);
        return page.Items;
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

    private async Task<ProductionOrderDto> CreateReleasedOrderAsync(HttpClient client)
    {
        var recipeResponse = await client.PostAsJsonAsync("/api/recipes", new
        {
            code = UniqueCode("R"),
            name = "Atomicity recipe",
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

        var releaseVersionResponse = await client.PostAsync($"/api/recipe-versions/{versionId}/release", null);
        releaseVersionResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

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

        var releaseOrderResponse = await client.PostAsync($"{OrdersUrl}/{order.Id}/release", null);
        releaseOrderResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadAsync<ProductionOrderDto>(releaseOrderResponse);
    }
}
