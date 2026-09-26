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
/// fan-out (issue #265). They exercise the full request pipeline against a
/// real PostgreSQL database and prove that the confirmation row, RW/PW
/// movements, genealogy edges and the order status flip commit together, and
/// that any failure leaves no partial rows behind.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ProductionConfirmationAtomicityEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/production-confirmations";
    private const string OrdersUrl = "/api/production-orders";
    private const string LotsUrl = "/api/lots";
    private const string GenealogyUrl = "/api/lot-genealogy";
    private const string MovementsUrl = "/api/stock-movements";

    [Fact]
    public async Task Create_WithLots_PersistsConfirmationMovementsEdgesAndOrderFlipTogether()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
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

        var movementsResponse = await client.GetAsync($"{MovementsUrl}?confirmationId={created.Id}");
        movementsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var movements = await ReadAsync<List<StockMovementDto>>(movementsResponse);
        movements.Should().NotBeEmpty();
        movements.Should().OnlyContain(m =>
            m.ProductionConfirmationId == created.Id && m.ProductionOrderId == order.Id);
        movements.Should().ContainSingle(m => m.MovementType == "PW" && m.Quantity == 10m);

        var edgesResponse = await client.GetAsync($"{GenealogyUrl}?productionConfirmationId={created.Id}");
        edgesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var edges = await ReadAsync<PagedResponseDto<LotGenealogyEdgeDto>>(edgesResponse);
        edges.Items.Should().ContainSingle(e =>
            e.ConsumedLotId == consumed.Id
            && e.ProducedLotId == produced.Id
            && e.ProductionOrderId == order.Id
            && e.ProductionConfirmationId == created.Id
            && e.MachineId == machineId
            && e.ConsumedQuantity == 5m);

        var orderResponse = await client.GetAsync($"{OrdersUrl}/{order.Id}");
        var fetchedOrder = await ReadAsync<ProductionOrderDto>(orderResponse);
        fetchedOrder.Status.Should().Be((short)ProductionOrderStatus.InProgress);
    }

    [Fact]
    public async Task Create_EdgePersistenceFailure_RollsBackConfirmationMovementsAndOrderFlip()
    {
        // Arrange — the consumed quantity passes handler validation (> 0, no
        // upper bound) but overflows the numeric(14,4) edge column, so the
        // edge insert fails after the confirmation row was written. The
        // transaction must roll the confirmation back too.
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);
        var produced = await CreateLotAsync(client);
        var consumed = await CreateLotAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(
            order.Id, producedLotId: produced.Id,
            consumedLots: new (Guid LotId, decimal Quantity)[] { (consumed.Id, 99999999999999m) }));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var browse = await client.GetAsync($"{BaseUrl}?productionOrderId={order.Id}");
        browse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<ProductionConfirmationDto>>(browse);
        page.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);

        var orderResponse = await client.GetAsync($"{OrdersUrl}/{order.Id}");
        var fetchedOrder = await ReadAsync<ProductionOrderDto>(orderResponse);
        fetchedOrder.Status.Should().Be((short)ProductionOrderStatus.Released);
    }

    [Fact]
    public async Task Create_UnknownConsumedLot_Returns404_AndLeavesNoPartialRows()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);
        var produced = await CreateLotAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(
            order.Id, producedLotId: produced.Id,
            consumedLots: new (Guid LotId, decimal Quantity)[] { (Guid.NewGuid(), 5m) }));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var browse = await client.GetAsync($"{BaseUrl}?productionOrderId={order.Id}");
        var page = await ReadAsync<PagedResponseDto<ProductionConfirmationDto>>(browse);
        page.Items.Should().BeEmpty();

        var orderResponse = await client.GetAsync($"{OrdersUrl}/{order.Id}");
        var fetchedOrder = await ReadAsync<ProductionOrderDto>(orderResponse);
        fetchedOrder.Status.Should().Be((short)ProductionOrderStatus.Released);
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

        var browse = await client.GetAsync($"{BaseUrl}?productionOrderId={order.Id}");
        var page = await ReadAsync<PagedResponseDto<ProductionConfirmationDto>>(browse);
        page.Items.Should().BeEmpty();
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

        var browse = await devClient.GetAsync($"{BaseUrl}?productionOrderId={order.Id}");
        var page = await ReadAsync<PagedResponseDto<ProductionConfirmationDto>>(browse);
        page.Items.Should().BeEmpty();
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
