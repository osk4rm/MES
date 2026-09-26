using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for soft material reservations
/// (issue #291, <c>/api/material-reservations</c>). They prove that releasing
/// an order allocates one Active reservation per BOM (product, warehouse)
/// pair, that confirmations relieve them inside the same transaction, that
/// closing settles the remainder, and that browse filters, paging, auth and
/// tenant isolation hold — against a real PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class MaterialReservationsEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/material-reservations";
    private const string OrdersUrl = "/api/production-orders";
    private const string ConfirmationsUrl = "/api/production-confirmations";

    // ReservationStatus: Active = 1, PartiallyRelieved = 2, Closed = 3.

    [Fact]
    public async Task Browse_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Release_WithBomItems_CreatesOneActiveReservationPerPair()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var finishedProductId = Guid.NewGuid();
        var setup = await CreateReleasedOrderWithBomAsync(client, finishedProductId);

        var response = await client.GetAsync($"{BaseUrl}?productionOrderId={setup.OrderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<MaterialReservationDto>>(response);
        page.TotalCount.Should().Be(2);
        // Per-unit 2 pcs over the planned 100 pcs with a warehouse hint.
        page.Items.Should().ContainSingle(x =>
            x.ProductionOrderId == setup.OrderId
            && x.ProductId == setup.PerUnitProductId
            && x.WarehouseId == setup.PreferredWarehouseId
            && x.QuantityReserved == 200m
            && x.QuantityRelieved == 0m
            && x.RemainingQuantity == 200m
            && x.Status == 1);
        // Per-batch 5 pcs plus 10% scrap, no warehouse hint.
        page.Items.Should().ContainSingle(x =>
            x.ProductionOrderId == setup.OrderId
            && x.ProductId == finishedProductId
            && x.WarehouseId == null
            && x.QuantityReserved == 5.5m
            && x.QuantityRelieved == 0m
            && x.RemainingQuantity == 5.5m
            && x.Status == 1);
    }

    [Fact]
    public async Task Release_WithoutBomItems_CreatesNoReservations()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var orderId = await CreateReleasedOrderWithoutBomAsync(client);

        var response = await client.GetAsync($"{BaseUrl}?productionOrderId={orderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<PagedResponseDto<MaterialReservationDto>>(response))
            .TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task ReRelease_Returns409_WithoutDuplicatingReservations()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var setup = await CreateReleasedOrderWithBomAsync(client, Guid.NewGuid());

        var rerelease = await client.PostAsync($"{OrdersUrl}/{setup.OrderId}/release", null);

        rerelease.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var browse = await client.GetAsync($"{BaseUrl}?productionOrderId={setup.OrderId}");
        (await ReadAsync<PagedResponseDto<MaterialReservationDto>>(browse))
            .TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Confirm_RelievesMatchingReservations()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var finishedProductId = Guid.NewGuid();
        var setup = await CreateReleasedOrderWithBomAsync(client, finishedProductId);

        var confirm = await client.PostAsJsonAsync(ConfirmationsUrl, ConfirmPayload(setup.OrderId, goodQuantity: 10m));
        confirm.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.GetAsync($"{BaseUrl}?productionOrderId={setup.OrderId}");
        var page = await ReadAsync<PagedResponseDto<MaterialReservationDto>>(response);
        // RW 20 pcs (2 per unit over 10 pcs) partially relieves the 200 pcs row.
        page.Items.Should().ContainSingle(x =>
            x.ProductId == setup.PerUnitProductId
            && x.QuantityRelieved == 20m
            && x.RemainingQuantity == 180m
            && x.Status == 2);
        // RW 5.5 pcs (per-batch plus scrap) fully relieves and closes its row.
        page.Items.Should().ContainSingle(x =>
            x.ProductId == finishedProductId
            && x.QuantityRelieved == 5.5m
            && x.RemainingQuantity == 0m
            && x.Status == 3);
    }

    [Fact]
    public async Task Get_ById_ReturnsReservation_AndUnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var setup = await CreateReleasedOrderWithBomAsync(client, Guid.NewGuid());
        var browse = await client.GetAsync($"{BaseUrl}?productionOrderId={setup.OrderId}");
        var existing = (await ReadAsync<PagedResponseDto<MaterialReservationDto>>(browse)).Items.First();

        var get = await client.GetAsync($"{BaseUrl}/{existing.Id}");

        get.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<MaterialReservationDto>(get)).Should().Be(existing);

        var unknown = await client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Browse_FiltersAndPaging_NarrowResults()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var setup = await CreateReleasedOrderWithBomAsync(client, Guid.NewGuid());

        var byProduct = await client.GetAsync($"{BaseUrl}?productId={setup.PerUnitProductId}");
        byProduct.StatusCode.Should().Be(HttpStatusCode.OK);
        var productPage = await ReadAsync<PagedResponseDto<MaterialReservationDto>>(byProduct);
        productPage.TotalCount.Should().Be(1);
        productPage.Items.Single().WarehouseId.Should().Be(setup.PreferredWarehouseId);

        var byWarehouse = await client.GetAsync($"{BaseUrl}?warehouseId={setup.PreferredWarehouseId}");
        (await ReadAsync<PagedResponseDto<MaterialReservationDto>>(byWarehouse))
            .TotalCount.Should().Be(1);

        var paged = await client.GetAsync($"{BaseUrl}?productionOrderId={setup.OrderId}&pageSize=1&pageNumber=2");
        paged.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondPage = await ReadAsync<PagedResponseDto<MaterialReservationDto>>(paged);
        secondPage.TotalCount.Should().Be(2);
        secondPage.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Browse_InvalidFilters_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var emptyGuid = await client.GetAsync($"{BaseUrl}?productId={Guid.Empty}");
        emptyGuid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var oversized = await client.GetAsync($"{BaseUrl}?pageSize=101");
        oversized.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Close_ClosesRemainingReservations()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var finishedProductId = Guid.NewGuid();
        var setup = await CreateReleasedOrderWithBomAsync(client, finishedProductId);

        var confirm = await client.PostAsJsonAsync(ConfirmationsUrl, ConfirmPayload(setup.OrderId, goodQuantity: 10m));
        confirm.StatusCode.Should().Be(HttpStatusCode.Created);

        var complete = await client.PostAsync($"{OrdersUrl}/{setup.OrderId}/complete", null);
        complete.StatusCode.Should().Be(HttpStatusCode.OK);

        var close = await client.PostAsync($"{OrdersUrl}/{setup.OrderId}/close", null);
        close.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.GetAsync($"{BaseUrl}?productionOrderId={setup.OrderId}");
        var page = await ReadAsync<PagedResponseDto<MaterialReservationDto>>(response);
        page.Items.Should().HaveCount(2);
        page.Items.Should().OnlyContain(x => x.Status == 3);

        // Closed reservations stop reducing availability: the per-unit pair
        // carries only its RW issue (-20 pcs) with nothing reserved.
        var stock = await client.GetAsync($"/api/stock-on-hand?productId={setup.PerUnitProductId}");
        stock.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<List<StockOnHandDto>>(stock)).Should().ContainSingle()
            .Which.Should().Be(new StockOnHandDto(
                setup.PerUnitProductId, setup.PreferredWarehouseId, -20m, 0m, -20m));
    }

    [Fact]
    public async Task CrossTenant_ReservationsAreInvisible()
    {
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var setup = await CreateReleasedOrderWithBomAsync(devClient, Guid.NewGuid());
        var browse = await devClient.GetAsync($"{BaseUrl}?productionOrderId={setup.OrderId}");
        var existing = (await ReadAsync<PagedResponseDto<MaterialReservationDto>>(browse)).Items.First();

        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);

        var filtered = await otherTenantClient.GetAsync($"{BaseUrl}?productionOrderId={setup.OrderId}");
        filtered.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<PagedResponseDto<MaterialReservationDto>>(filtered)).TotalCount.Should().Be(0);

        var direct = await otherTenantClient.GetAsync($"{BaseUrl}/{existing.Id}");
        direct.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record OrderSetup(
        Guid OrderId,
        Guid PerUnitProductId,
        Guid PerBatchProductId,
        Guid PreferredWarehouseId);

    private sealed record OperationNodeDto(Guid Id);

    private static string UniqueCode(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    private static object ConfirmPayload(Guid orderId, decimal goodQuantity) => new
    {
        productionOrderId = orderId,
        machineId = Guid.NewGuid(),
        reportedByOperatorId = (Guid?)null,
        reportedAt = DateTime.UtcNow,
        goodQuantity,
        scrapQuantity = 0m,
        notes = (string?)null
    };

    /// <summary>
    /// Builds a real recipe with one operation and two BOM items (a PerUnit
    /// item of 2 units with a preferred warehouse hint, and a PerBatch item
    /// of 5 units with 10% scrap and no warehouse hint), releases the
    /// version, creates an order for <paramref name="orderProductId"/> against
    /// it and releases the order (planned quantity 100).
    /// </summary>
    private async Task<OrderSetup> CreateReleasedOrderWithBomAsync(HttpClient client, Guid orderProductId)
    {
        var draft = await CreateDraftVersionWithOperationAsync(client);
        var perUnitProductId = Guid.NewGuid();
        var preferredWarehouseId = Guid.NewGuid();

        var bomPerUnit = await client.PostAsJsonAsync($"/api/operations/{draft.OperationId}/bom-items", new
        {
            operationId = draft.OperationId,
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

        var bomPerBatch = await client.PostAsJsonAsync($"/api/operations/{draft.OperationId}/bom-items", new
        {
            operationId = draft.OperationId,
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

        await ReleaseVersionAsync(client, draft.VersionId);
        var orderId = await CreateAndReleaseOrderAsync(client, draft.RecipeId, draft.VersionId, orderProductId);

        return new OrderSetup(orderId, perUnitProductId, orderProductId, preferredWarehouseId);
    }

    /// <summary>
    /// Builds a recipe with one operation but no BOM items, releases the
    /// version and releases an order against it.
    /// </summary>
    private async Task<Guid> CreateReleasedOrderWithoutBomAsync(HttpClient client)
    {
        var draft = await CreateDraftVersionWithOperationAsync(client);

        await ReleaseVersionAsync(client, draft.VersionId);
        return await CreateAndReleaseOrderAsync(client, draft.RecipeId, draft.VersionId, Guid.NewGuid());
    }

    private sealed record DraftVersion(Guid RecipeId, Guid VersionId, Guid OperationId);

    private async Task<DraftVersion> CreateDraftVersionWithOperationAsync(HttpClient client)
    {
        var recipeResponse = await client.PostAsJsonAsync("/api/recipes", new
        {
            code = UniqueCode("R"),
            name = "Reservation recipe",
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
        var operationId = (await ReadAsync<OperationNodeDto>(operationResponse)).Id;

        return new DraftVersion(recipe.Id, versionId, operationId);
    }

    private static async Task ReleaseVersionAsync(HttpClient client, Guid versionId)
    {
        var release = await client.PostAsync($"/api/recipe-versions/{versionId}/release", null);
        release.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<Guid> CreateAndReleaseOrderAsync(
        HttpClient client, Guid recipeId, Guid versionId, Guid orderProductId)
    {
        var orderResponse = await client.PostAsJsonAsync(OrdersUrl, new
        {
            code = UniqueCode("PO"),
            productId = orderProductId,
            recipeId,
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

        return (await ReadAsync<ProductionOrderDto>(releaseOrder)).Id;
    }
}
