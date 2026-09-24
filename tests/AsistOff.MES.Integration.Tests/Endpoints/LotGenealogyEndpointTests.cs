using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>/api/lot-genealogy</c>. They
/// exercise the full request pipeline: authentication, tenant resolution,
/// MediatR handlers, EF Core persistence and the global exception handler -
/// against a real PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class LotGenealogyEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/lot-genealogy";
    private const string LotsUrl = "/api/lots";
    private const string OrdersUrl = "/api/production-orders";
    private const string ConfirmationsUrl = "/api/production-confirmations";

    [Fact]
    public async Task Browse_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Record_ReturnsCreated_AndIsBrowsableByProducedLot()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var consumed = await CreateLotAsync(client);
        var produced = await CreateLotAsync(client);
        var order = await CreateOrderAsync(client);

        var createResponse = await client.PostAsJsonAsync(BaseUrl, EdgePayload(consumed.Id, produced.Id, order.Id));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<LotGenealogyEdgeDto>(createResponse);
        created.ConsumedLotId.Should().Be(consumed.Id);
        created.ProducedLotId.Should().Be(produced.Id);
        created.ProductionOrderId.Should().Be(order.Id);
        created.ConsumedQuantity.Should().Be(5m);

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var browseResponse = await client.GetAsync($"{BaseUrl}?producedLotId={produced.Id}");

        browseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<LotGenealogyEdgeDto>>(browseResponse);
        page.Items.Should().ContainSingle(item => item.Id == created.Id);
    }

    [Fact]
    public async Task Browse_ByProducedLotId_ReturnsCallerTenantRowsOnly()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var consumed = await CreateLotAsync(client);
        var produced = await CreateLotAsync(client);
        var order = await CreateOrderAsync(client);
        var create = await client.PostAsJsonAsync(BaseUrl, EdgePayload(consumed.Id, produced.Id, order.Id));
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);

        var crossTenantResponse = await otherTenantClient.GetAsync($"{BaseUrl}?producedLotId={produced.Id}");

        crossTenantResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var crossTenantPage = await ReadAsync<PagedResponseDto<LotGenealogyEdgeDto>>(crossTenantResponse);
        crossTenantPage.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Browse_SupportsConsumedLotAndOrderFilters()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var consumed = await CreateLotAsync(client);
        var produced = await CreateLotAsync(client);
        var otherProduced = await CreateLotAsync(client);
        var order = await CreateOrderAsync(client);
        var otherOrder = await CreateOrderAsync(client);
        var first = await ReadAsync<LotGenealogyEdgeDto>(await client.PostAsJsonAsync(
            BaseUrl, EdgePayload(consumed.Id, produced.Id, order.Id)));
        var second = await ReadAsync<LotGenealogyEdgeDto>(await client.PostAsJsonAsync(
            BaseUrl, EdgePayload(consumed.Id, otherProduced.Id, otherOrder.Id)));

        var byConsumed = await client.GetAsync($"{BaseUrl}?consumedLotId={consumed.Id}");

        byConsumed.StatusCode.Should().Be(HttpStatusCode.OK);
        var byConsumedPage = await ReadAsync<PagedResponseDto<LotGenealogyEdgeDto>>(byConsumed);
        byConsumedPage.Items.Select(i => i.Id).Should().Contain([first.Id, second.Id]);

        var byOrder = await client.GetAsync($"{BaseUrl}?productionOrderId={order.Id}");

        byOrder.StatusCode.Should().Be(HttpStatusCode.OK);
        var byOrderPage = await ReadAsync<PagedResponseDto<LotGenealogyEdgeDto>>(byOrder);
        byOrderPage.Items.Should().ContainSingle(item => item.Id == first.Id);
        byOrderPage.Items.Should().NotContain(item => item.Id == second.Id);
    }

    [Fact]
    public async Task Record_CrossTenantLot_Returns404()
    {
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var foreignLot = await CreateLotAsync(devClient);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var consumed = await CreateLotAsync(client);
        var order = await CreateOrderAsync(client);

        var response = await client.PostAsJsonAsync(BaseUrl, EdgePayload(consumed.Id, foreignLot.Id, order.Id));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Record_UnknownLot_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var produced = await CreateLotAsync(client);
        var order = await CreateOrderAsync(client);

        var response = await client.PostAsJsonAsync(BaseUrl, EdgePayload(Guid.NewGuid(), produced.Id, order.Id));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Record_UnknownOrder_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var consumed = await CreateLotAsync(client);
        var produced = await CreateLotAsync(client);

        var response = await client.PostAsJsonAsync(BaseUrl, EdgePayload(consumed.Id, produced.Id, Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Record_ZeroQuantity_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var consumed = await CreateLotAsync(client);
        var produced = await CreateLotAsync(client);
        var order = await CreateOrderAsync(client);

        var response = await client.PostAsJsonAsync(
            BaseUrl, EdgePayload(consumed.Id, produced.Id, order.Id, consumedQuantity: 0m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Record_SelfLink_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var lot = await CreateLotAsync(client);
        var order = await CreateOrderAsync(client);

        var response = await client.PostAsJsonAsync(BaseUrl, EdgePayload(lot.Id, lot.Id, order.Id));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Record_FutureOccurredAt_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var consumed = await CreateLotAsync(client);
        var produced = await CreateLotAsync(client);
        var order = await CreateOrderAsync(client);

        var response = await client.PostAsJsonAsync(
            BaseUrl, EdgePayload(consumed.Id, produced.Id, order.Id, occurredAt: DateTime.UtcNow.AddHours(2)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Record_ConfirmationFromAnotherOrder_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var releasedOrder = await CreateReleasedOrderAsync(client);
        var confirmation = await CreateConfirmationAsync(client, releasedOrder.Id);
        var consumed = await CreateLotAsync(client);
        var produced = await CreateLotAsync(client);
        var otherOrder = await CreateOrderAsync(client);

        var response = await client.PostAsJsonAsync(
            BaseUrl, EdgePayload(consumed.Id, produced.Id, otherOrder.Id, productionConfirmationId: confirmation.Id));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Record_WithMatchingConfirmation_ReturnsCreated()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var releasedOrder = await CreateReleasedOrderAsync(client);
        var confirmation = await CreateConfirmationAsync(client, releasedOrder.Id);
        var consumed = await CreateLotAsync(client);
        var produced = await CreateLotAsync(client);

        var response = await client.PostAsJsonAsync(
            BaseUrl, EdgePayload(consumed.Id, produced.Id, releasedOrder.Id, productionConfirmationId: confirmation.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<LotGenealogyEdgeDto>(response);
        created.ProductionConfirmationId.Should().Be(confirmation.Id);
    }

    [Fact]
    public async Task Delete_KnownEdge_Returns204_AndUnknownReturns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var consumed = await CreateLotAsync(client);
        var produced = await CreateLotAsync(client);
        var order = await CreateOrderAsync(client);
        var create = await client.PostAsJsonAsync(BaseUrl, EdgePayload(consumed.Id, produced.Id, order.Id));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<LotGenealogyEdgeDto>(create);

        var deleteResponse = await client.DeleteAsync($"{BaseUrl}/{created.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var unknownDelete = await client.DeleteAsync($"{BaseUrl}/{Guid.NewGuid()}");

        unknownDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("upstream")]
    [InlineData("downstream")]
    public async Task Traceability_WithoutToken_Returns401(string direction)
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync($"{BaseUrl}/{direction}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Upstream_TwoLevelChain_ReturnsBothLevels()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var raw = await CreateLotAsync(client);
        var mid = await CreateLotAsync(client);
        var finished = await CreateLotAsync(client);
        var order = await CreateOrderAsync(client);
        var machineId = Guid.NewGuid();

        var first = await client.PostAsJsonAsync(BaseUrl, EdgePayload(raw.Id, mid.Id, order.Id, machineId: machineId));
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var second = await client.PostAsJsonAsync(BaseUrl, EdgePayload(mid.Id, finished.Id, order.Id, consumedQuantity: 7m));
        second.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.GetAsync($"{BaseUrl}/upstream/{finished.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var trace = await ReadAsync<LotTraceabilityDto>(response);
        trace.RootLotId.Should().Be(finished.Id);
        trace.Truncated.Should().BeFalse();
        trace.Nodes.Should().HaveCount(2);
        trace.Nodes.Single(n => n.LotId == mid.Id).Depth.Should().Be(1);
        trace.Nodes.Single(n => n.LotId == mid.Id).ConsumedQuantity.Should().Be(7m);
        trace.Nodes.Single(n => n.LotId == mid.Id).ProductionOrderCode.Should().Be(order.Code);
        trace.Nodes.Single(n => n.LotId == raw.Id).Depth.Should().Be(2);
        trace.Nodes.Single(n => n.LotId == raw.Id).MachineId.Should().Be(machineId);
        trace.Nodes.Should().OnlyContain(n => n.OccurredAt > DateTime.MinValue);
        trace.Nodes.Should().OnlyContain(n => !string.IsNullOrWhiteSpace(n.LotCode));
        trace.Nodes.Should().OnlyContain(n => !string.IsNullOrWhiteSpace(n.ProductionOrderCode));
    }

    [Fact]
    public async Task Downstream_TwoLevelChain_ReturnsTransitiveLots()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var raw = await CreateLotAsync(client);
        var mid = await CreateLotAsync(client);
        var finished = await CreateLotAsync(client);
        var order = await CreateOrderAsync(client);

        (await client.PostAsJsonAsync(BaseUrl, EdgePayload(raw.Id, mid.Id, order.Id))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await client.PostAsJsonAsync(BaseUrl, EdgePayload(mid.Id, finished.Id, order.Id))).StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.GetAsync($"{BaseUrl}/downstream/{raw.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var trace = await ReadAsync<LotTraceabilityDto>(response);
        trace.RootLotId.Should().Be(raw.Id);
        trace.Truncated.Should().BeFalse();
        trace.Nodes.Select(n => n.LotId).Should().BeEquivalentTo([mid.Id, finished.Id]);
        trace.Nodes.Single(n => n.LotId == mid.Id).Depth.Should().Be(1);
        trace.Nodes.Single(n => n.LotId == finished.Id).Depth.Should().Be(2);
    }

    [Fact]
    public async Task Downstream_Cycle_TerminatesAndListsEachLotOnce()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var a = await CreateLotAsync(client);
        var b = await CreateLotAsync(client);
        var order = await CreateOrderAsync(client);

        (await client.PostAsJsonAsync(BaseUrl, EdgePayload(a.Id, b.Id, order.Id))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await client.PostAsJsonAsync(BaseUrl, EdgePayload(b.Id, a.Id, order.Id))).StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.GetAsync($"{BaseUrl}/downstream/{a.Id}?maxDepth=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var trace = await ReadAsync<LotTraceabilityDto>(response);
        trace.Nodes.Select(n => n.LotId).Should().OnlyHaveUniqueItems();
        trace.Nodes.Should().ContainSingle(n => n.LotId == b.Id);
    }

    [Fact]
    public async Task Upstream_CrossTenantRoot_Returns404()
    {
        using var ownerClient = await Fixture.CreateAuthenticatedClientAsync();
        var foreignLot = await CreateLotAsync(ownerClient);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);

        var response = await client.GetAsync($"{BaseUrl}/upstream/{foreignLot.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Downstream_CrossTenantRoot_Returns404()
    {
        using var ownerClient = await Fixture.CreateAuthenticatedClientAsync();
        var foreignLot = await CreateLotAsync(ownerClient);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);

        var response = await client.GetAsync($"{BaseUrl}/downstream/{foreignLot.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Upstream_UnknownLot_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{BaseUrl}/upstream/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("upstream")]
    [InlineData("downstream")]
    public async Task Traceability_InvalidDepth_Returns400(string direction)
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var lot = await CreateLotAsync(client);

        foreach (var depth in new[] { "0", "11" })
        {
            var response = await client.GetAsync($"{BaseUrl}/{direction}/{lot.Id}?maxDepth={depth}");

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }

    private static object EdgePayload(
        Guid consumedLotId,
        Guid producedLotId,
        Guid productionOrderId,
        Guid? productionConfirmationId = null,
        decimal consumedQuantity = 5m,
        DateTime? occurredAt = null,
        Guid? machineId = null) => new
        {
            consumedLotId,
            producedLotId,
            productionOrderId,
            productionConfirmationId,
            machineId = machineId ?? Guid.NewGuid(),
            reportedByOperatorId = (Guid?)null,
            consumedQuantity,
            occurredAt = occurredAt ?? DateTime.UtcNow,
            notes = (string?)null
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

    private static async Task<ProductionOrderDto> CreateOrderAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(OrdersUrl, new
        {
            code = UniqueCode("PO"),
            productId = Guid.NewGuid(),
            recipeId = Guid.NewGuid(),
            recipeVersionId = Guid.NewGuid(),
            plannedQuantity = 100m,
            measureUnitId = (Guid?)null,
            priority = 0,
            dueDate = (DateTime?)null,
            notes = (string?)null,
            syncId = (string?)null
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<ProductionOrderDto>(response);
    }

    private static async Task<ProductionConfirmationDto> CreateConfirmationAsync(HttpClient client, Guid orderId)
    {
        var response = await client.PostAsJsonAsync(ConfirmationsUrl, new
        {
            productionOrderId = orderId,
            machineId = Guid.NewGuid(),
            reportedByOperatorId = (Guid?)null,
            reportedAt = DateTime.UtcNow,
            goodQuantity = 10m,
            scrapQuantity = 0m,
            notes = (string?)null
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<ProductionConfirmationDto>(response);
    }

    /// <summary>
    /// Builds a real recipe with one operation, releases its version, creates an
    /// order against it and releases the order, so confirmations can be reported.
    /// </summary>
    private static async Task<ProductionOrderDto> CreateReleasedOrderAsync(HttpClient client)
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
