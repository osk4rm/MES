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
/// Atomicity coverage for the operator confirmation fan-out (issue #265).
/// The confirmation row, all RW/PW movements, all genealogy edges and the
/// order status flip commit together: the happy path reads all four back in
/// one reload, while validation failures (404/409/400) leave no partial rows
/// behind. True mid-fan-out persistence failures are covered at unit level
/// (<c>CreateProductionConfirmationAtomicityTests</c>) with a failing
/// repository behind the same <c>IUnitOfWork</c> boundary.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ProductionConfirmationAtomicityEndpointTests(MesApplicationFixture fixture)
    : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/production-confirmations";
    private const string OrdersUrl = "/api/production-orders";
    private const string MovementsUrl = "/api/stock-movements";
    private const string GenealogyUrl = "/api/lot-genealogy";
    private const string LotsUrl = "/api/lots";

    [Fact]
    public async Task Create_ValidConfirmation_PersistsConfirmationMovementsAndOrderFlipTogether()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);

        // Act
        var createResponse = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(order.Id));

        // Assert
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ProductionConfirmationDto>(createResponse);

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var movementsResponse = await client.GetAsync($"{MovementsUrl}?confirmationId={created.Id}");
        movementsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var lines = await ReadAsync<List<StockMovementDto>>(movementsResponse);
        lines.Should().NotBeEmpty();
        lines.Should().OnlyContain(l =>
            l.ProductionConfirmationId == created.Id && l.ProductionOrderId == order.Id);
        lines.Should().ContainSingle(l => l.MovementType == "PW" && l.Quantity == 10m);

        var orderResponse = await client.GetAsync($"{OrdersUrl}/{order.Id}");
        var fetchedOrder = await ReadAsync<ProductionOrderDto>(orderResponse);
        fetchedOrder.Status.Should().Be((short)ProductionOrderStatus.InProgress);
    }

    [Fact]
    public async Task Create_WithLots_PersistsGenealogyEdgesWithConfirmation()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);
        var produced = await CreateLotAsync(client);
        var consumed = await CreateLotAsync(client);

        // Act
        var createResponse = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(
            order.Id, producedLotId: produced.Id,
            consumedLots: new (Guid LotId, decimal Quantity)[] { (consumed.Id, 5m) }));

        // Assert
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ProductionConfirmationDto>(createResponse);

        var edgesResponse = await client.GetAsync($"{GenealogyUrl}?productionConfirmationId={created.Id}");
        edgesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<LotGenealogyEdgeDto>>(edgesResponse);
        page.Items.Should().ContainSingle(i =>
            i.ProducedLotId == produced.Id
            && i.ConsumedLotId == consumed.Id
            && i.ProductionConfirmationId == created.Id
            && i.ProductionOrderId == order.Id);
    }

    [Fact]
    public async Task Create_UnknownLot_Returns404_AndLeavesNoPartialRows()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);
        var consumed = await CreateLotAsync(client);

        // Act — produced lot does not exist, so lot resolution fails fast
        // with 404 before any write.
        var response = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(
            order.Id, producedLotId: Guid.NewGuid(),
            consumedLots: new (Guid LotId, decimal Quantity)[] { (consumed.Id, 5m) }));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CountRowsForOrderAsync(order.Id)).Should().Be((0, 0, 0));
    }

    [Fact]
    public async Task Create_ClosedOrder_Returns409_AndLeavesNoPartialRows()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);
        await SetOrderStatusAsync(order.Id, ProductionOrderStatus.Closed);

        // Act
        var response = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(order.Id));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CountRowsForOrderAsync(order.Id)).Should().Be((0, 0, 0));
    }

    [Fact]
    public async Task Create_CrossTenantOrder_Returns404_AndWritesNothing()
    {
        // Arrange
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(devClient);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);

        // Act — the order is hidden behind the tenant query filter.
        var response = await otherTenantClient.PostAsJsonAsync(BaseUrl, ConfirmPayload(order.Id));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var browse = await devClient.GetAsync($"{BaseUrl}?productionOrderId={order.Id}");
        browse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<ProductionConfirmationDto>>(browse);
        page.Items.Should().BeEmpty();
    }

    private async Task<(int Confirmations, int Movements, int Edges)> CountRowsForOrderAsync(Guid orderId)
    {
        using var scope = Fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();

        var confirmations = await context.Set<ProductionConfirmation>()
            .IgnoreQueryFilters()
            .CountAsync(x => x.ProductionOrderId == orderId);
        var movements = await context.Set<StockMovement>()
            .IgnoreQueryFilters()
            .CountAsync(x => x.ProductionOrderId == orderId);
        var edges = await context.Set<LotGenealogyEdge>()
            .IgnoreQueryFilters()
            .CountAsync(x => x.ProductionOrderId == orderId);

        return (confirmations, movements, edges);
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

    private async Task<ProductionOrderDto> CreateOrderAsync(
        HttpClient client, Guid? recipeId = null, Guid? recipeVersionId = null)
    {
        var payload = new
        {
            code = UniqueCode("PO"),
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

        var response = await client.PostAsJsonAsync(OrdersUrl, payload);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<ProductionOrderDto>(response);
    }

    private async Task<ProductionOrderDto> CreateReleasedOrderAsync(HttpClient client)
    {
        var recipeResponse = await client.PostAsJsonAsync("/api/recipes", new
        {
            code = UniqueCode("R"),
            name = "Integration recipe",
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

        var order = await CreateOrderAsync(client, recipe.Id, versionId);

        var releaseOrderResponse = await client.PostAsync($"{OrdersUrl}/{order.Id}/release", null);
        releaseOrderResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadAsync<ProductionOrderDto>(releaseOrderResponse);
    }
}
