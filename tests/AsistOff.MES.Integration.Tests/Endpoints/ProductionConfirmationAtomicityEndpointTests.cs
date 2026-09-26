using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Atomicity tests for the operator confirmation fan-out (issue #265). The
/// confirmation row, RW/PW movements, genealogy edges and the order status
/// flip commit in one transaction: the happy path reads all four back in one
/// reload, while 404/409/400 rejections leave zero confirmation, movement
/// and edge rows behind.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ProductionConfirmationAtomicityEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/production-confirmations";
    private const string OrdersUrl = "/api/production-orders";
    private const string MovementsUrl = "/api/stock-movements";
    private const string GenealogyUrl = "/api/lot-genealogy";
    private const string LotsUrl = "/api/lots";

    [Fact]
    public async Task Create_HappyPath_PersistsConfirmationMovementsEdgesAndStatusTogether()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);
        var produced = await CreateLotAsync(client);
        var consumed = await CreateLotAsync(client);
        var machineId = Guid.NewGuid();

        // Act — one POST fans out to all four writes.
        var createResponse = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(
            order.Id, machineId: machineId, producedLotId: produced.Id,
            consumedLots: [(consumed.Id, 5m)]));

        // Assert — readable in one reload: confirmation plus movements plus
        // edges plus the Released -> InProgress flip, with persisted movement
        // lines exactly matching the preview lines.
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ProductionConfirmationDto>(createResponse);

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var previewResponse = await client.GetAsync($"{BaseUrl}/{created.Id}/movements");
        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = await ReadAsync<List<MovementPreviewLineDto>>(previewResponse);

        var persistedResponse = await client.GetAsync($"{MovementsUrl}?confirmationId={created.Id}");
        persistedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var persisted = await ReadAsync<List<StockMovementDto>>(persistedResponse);
        persisted.Should().HaveCount(preview.Count);
        foreach (var expected in preview)
        {
            persisted.Should().ContainSingle(l =>
                l.MovementType == expected.MovementType
                && l.ProductId == expected.ProductId
                && l.Quantity == expected.Quantity
                && l.MeasureUnitId == expected.MeasureUnitId
                && l.WarehouseId == expected.PreferredWarehouseId);
        }
        persisted.Should().OnlyContain(l =>
            l.ProductionConfirmationId == created.Id && l.ProductionOrderId == order.Id);

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
    public async Task Create_MidFanOutFailure_RollsBackConfirmationMovementsAndEdges()
    {
        // Arrange — real Released order plus real lots via HTTP so the tenant
        // and FK targets are valid. Then replicate the handler fan-out order
        // (confirmation, movements, edges) inside one real IUnitOfWork
        // transaction against the Testcontainers PostgreSQL and fail before
        // the edge/status writes, simulating a mid-fan-out edge failure.
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);
        _ = await CreateLotAsync(client);
        _ = await CreateLotAsync(client);

        Guid tenantId;
        using (var scope = Fixture.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
            var orderRow = await context.Set<ProductionOrder>()
                .IgnoreQueryFilters()
                .FirstAsync(x => x.Id == order.Id);
            tenantId = orderRow.TenantId;
        }

        var confirmationId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var confirmation = new ProductionConfirmation
        {
            Id = confirmationId,
            TenantId = tenantId,
            ProductionOrderId = order.Id,
            MachineId = Guid.NewGuid(),
            ReportedAt = now,
            GoodQuantity = 10m,
            ScrapQuantity = 0m,
            CreatedAt = now
        };
        var movement = new StockMovement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MovementType = StockMovement.ReceiptType,
            ProductId = Guid.NewGuid(),
            Quantity = 10m,
            ProductionConfirmationId = confirmationId,
            ProductionOrderId = order.Id,
            ReportedAt = now,
            CreatedAt = now
        };

        // Act
        using (var scope = Fixture.Services.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var confirmations = scope.ServiceProvider.GetRequiredService<IProductionConfirmationsRepository>();
            var movements = scope.ServiceProvider.GetRequiredService<IStockMovementsRepository>();

            var act = () => unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await confirmations.AddAsync(confirmation);
                await movements.AddRangeAsync(new[] { movement });
                throw new InvalidOperationException("simulated genealogy edge insert failure");
            });

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        // Assert — no orphan rows by DB count, nothing in browse, nothing by
        // id, and the order is still Released.
        (await CountConfirmationsAsync(order.Id)).Should().Be(0);
        (await CountMovementsForOrderAsync(order.Id)).Should().Be(0);
        (await CountEdgesForOrderAsync(order.Id)).Should().Be(0);

        var browse = await client.GetAsync($"{BaseUrl}?productionOrderId={order.Id}");
        browse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<ProductionConfirmationDto>>(browse);
        page.Items.Should().BeEmpty();

        var getById = await client.GetAsync($"{BaseUrl}/{confirmationId}");
        getById.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var orderResponse = await client.GetAsync($"{OrdersUrl}/{order.Id}");
        orderResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetchedOrder = await ReadAsync<ProductionOrderDto>(orderResponse);
        fetchedOrder.Status.Should().Be((short)ProductionOrderStatus.Released);
    }

    [Fact]
    public async Task Create_UnknownLot_Returns404_AndWritesNothing()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);
        var consumed = await CreateLotAsync(client);

        // Act — lot resolution fails before any write.
        var response = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(
            order.Id, producedLotId: Guid.NewGuid(),
            consumedLots: [(consumed.Id, 5m)]));

        // Assert — 404 with no partial rows left behind.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CountConfirmationsAsync(order.Id)).Should().Be(0);
        (await CountMovementsForOrderAsync(order.Id)).Should().Be(0);
        (await CountEdgesForOrderAsync(order.Id)).Should().Be(0);

        var browse = await client.GetAsync($"{BaseUrl}?productionOrderId={order.Id}");
        var page = await ReadAsync<PagedResponseDto<ProductionConfirmationDto>>(browse);
        page.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData(ProductionOrderStatus.Closed)]
    [InlineData(ProductionOrderStatus.Completed)]
    public async Task Create_FinalOrder_Returns409_AndWritesNothing(ProductionOrderStatus status)
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);
        await SetOrderStatusAsync(order.Id, status);

        // Act
        var response = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(order.Id));

        // Assert — 409 with no partial rows left behind.
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CountConfirmationsAsync(order.Id)).Should().Be(0);
        (await CountMovementsForOrderAsync(order.Id)).Should().Be(0);
        (await CountEdgesForOrderAsync(order.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Create_BadQuantity_Returns400_AndWritesNothing()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(
            BaseUrl, ConfirmPayload(order.Id, goodQuantity: 0m, scrapQuantity: 0m));

        // Assert — 400 with no partial rows left behind.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CountConfirmationsAsync(order.Id)).Should().Be(0);
        (await CountMovementsForOrderAsync(order.Id)).Should().Be(0);
        (await CountEdgesForOrderAsync(order.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Create_CrossTenantOrder_Returns404_AndWritesNothing()
    {
        // Arrange
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(devClient);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);

        // Act — cross-tenant rows are hidden by the global query filter.
        var response = await otherTenantClient.PostAsJsonAsync(BaseUrl, ConfirmPayload(order.Id));

        // Assert — 404 and the foreign tenant sees nothing.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var crossTenantBrowse = await otherTenantClient.GetAsync($"{BaseUrl}?productionOrderId={order.Id}");
        var crossTenantPage = await ReadAsync<PagedResponseDto<ProductionConfirmationDto>>(crossTenantBrowse);
        crossTenantPage.Items.Should().BeEmpty();

        (await CountConfirmationsAsync(order.Id)).Should().Be(0);
        (await CountMovementsForOrderAsync(order.Id)).Should().Be(0);
        (await CountEdgesForOrderAsync(order.Id)).Should().Be(0);
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
