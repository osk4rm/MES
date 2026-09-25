using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for automatic lot genealogy derivation
/// on confirmation (issue #207). They exercise the full request pipeline:
/// authentication, tenant resolution, MediatR handlers, EF Core persistence
/// and the global exception handler - against a real PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ProductionConfirmationGenealogyEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/production-confirmations";
    private const string GenealogyUrl = "/api/lot-genealogy";
    private const string LotsUrl = "/api/lots";
    private const string OrdersUrl = "/api/production-orders";

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_WithProducedAndTwoConsumedLots_PostsTwoEdgesReadableByConfirmation()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);
        var produced = await CreateLotAsync(client);
        var consumedA = await CreateLotAsync(client);
        var consumedB = await CreateLotAsync(client);
        var machineId = Guid.NewGuid();

        var createResponse = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(
            order.Id, machineId: machineId, producedLotId: produced.Id,
            consumedLots: new (Guid LotId, decimal Quantity)[]
            {
                (consumedA.Id, 5m),
                (consumedB.Id, 7m),
            }));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ProductionConfirmationDto>(createResponse);

        var browseResponse = await client.GetAsync($"{GenealogyUrl}?productionConfirmationId={created.Id}");

        browseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<LotGenealogyEdgeDto>>(browseResponse);
        page.Items.Should().HaveCount(2);
        page.Items.Select(i => i.ConsumedLotId).Should().BeEquivalentTo([consumedA.Id, consumedB.Id]);
        page.Items.Select(i => i.ConsumedQuantity).Should().BeEquivalentTo([5m, 7m]);
        page.Items.Should().OnlyContain(i =>
            i.ProducedLotId == produced.Id
            && i.ProductionOrderId == order.Id
            && i.ProductionConfirmationId == created.Id
            && i.MachineId == machineId);
        foreach (var item in page.Items)
            item.OccurredAt.Should().BeCloseTo(created.ReportedAt, TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task Create_WithoutLotReferences_PostsNoEdges()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);

        var createResponse = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(order.Id));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ProductionConfirmationDto>(createResponse);

        var browseResponse = await client.GetAsync($"{GenealogyUrl}?productionConfirmationId={created.Id}");

        browseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<LotGenealogyEdgeDto>>(browseResponse);
        page.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_SelfLink_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);
        var lot = await CreateLotAsync(client);

        var response = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(
            order.Id, producedLotId: lot.Id,
            consumedLots: new (Guid LotId, decimal Quantity)[] { (lot.Id, 5m) }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2.5)]
    public async Task Create_NonPositiveConsumedQuantity_Returns400(decimal quantity)
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);
        var produced = await CreateLotAsync(client);
        var consumed = await CreateLotAsync(client);

        var response = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(
            order.Id, producedLotId: produced.Id,
            consumedLots: new (Guid LotId, decimal Quantity)[] { (consumed.Id, quantity) }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_ConsumedLotsWithoutProducedLot_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);
        var consumed = await CreateLotAsync(client);

        var response = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(
            order.Id,
            consumedLots: new (Guid LotId, decimal Quantity)[] { (consumed.Id, 5m) }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_UnknownProducedLot_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);
        var consumed = await CreateLotAsync(client);

        var response = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(
            order.Id, producedLotId: Guid.NewGuid(),
            consumedLots: new (Guid LotId, decimal Quantity)[] { (consumed.Id, 5m) }));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_UnknownConsumedLot_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);
        var produced = await CreateLotAsync(client);

        var response = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(
            order.Id, producedLotId: produced.Id,
            consumedLots: new (Guid LotId, decimal Quantity)[] { (Guid.NewGuid(), 5m) }));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_CrossTenantLot_Returns404()
    {
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var foreignLot = await CreateLotAsync(devClient);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var order = await CreateReleasedOrderAsync(client);
        var consumed = await CreateLotAsync(client);

        var response = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(
            order.Id, producedLotId: foreignLot.Id,
            consumedLots: new (Guid LotId, decimal Quantity)[] { (consumed.Id, 5m) }));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Browse_ByConfirmationId_ReturnsCallerTenantRowsOnly()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);
        var produced = await CreateLotAsync(client);
        var consumed = await CreateLotAsync(client);
        var create = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(
            order.Id, producedLotId: produced.Id,
            consumedLots: new (Guid LotId, decimal Quantity)[] { (consumed.Id, 5m) }));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ProductionConfirmationDto>(create);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);

        var crossTenantResponse = await otherTenantClient.GetAsync(
            $"{GenealogyUrl}?productionConfirmationId={created.Id}");

        crossTenantResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var crossTenantPage = await ReadAsync<PagedResponseDto<LotGenealogyEdgeDto>>(crossTenantResponse);
        crossTenantPage.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_Confirmation_DeletesAutoPostedEdges_AndRecreatePostsFreshSet()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);
        var produced = await CreateLotAsync(client);
        var consumed = await CreateLotAsync(client);
        var create = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(
            order.Id, producedLotId: produced.Id,
            consumedLots: new (Guid LotId, decimal Quantity)[] { (consumed.Id, 5m) }));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ProductionConfirmationDto>(create);

        var beforeDelete = await ReadAsync<PagedResponseDto<LotGenealogyEdgeDto>>(
            await client.GetAsync($"{GenealogyUrl}?productionConfirmationId={created.Id}"));
        beforeDelete.Items.Should().HaveCount(1);

        var deleteResponse = await client.DeleteAsync($"{BaseUrl}/{created.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDelete = await ReadAsync<PagedResponseDto<LotGenealogyEdgeDto>>(
            await client.GetAsync($"{GenealogyUrl}?productionConfirmationId={created.Id}"));
        afterDelete.Items.Should().BeEmpty();

        var recreate = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(
            order.Id, producedLotId: produced.Id,
            consumedLots: new (Guid LotId, decimal Quantity)[] { (consumed.Id, 5m) }));

        recreate.StatusCode.Should().Be(HttpStatusCode.Created);
        var recreated = await ReadAsync<ProductionConfirmationDto>(recreate);
        recreated.Id.Should().NotBe(created.Id);

        var afterRecreate = await ReadAsync<PagedResponseDto<LotGenealogyEdgeDto>>(
            await client.GetAsync($"{GenealogyUrl}?productionConfirmationId={recreated.Id}"));
        afterRecreate.Items.Should().HaveCount(1);
        afterRecreate.Items.Single().ProductionConfirmationId.Should().Be(recreated.Id);
    }

    [Fact]
    public async Task Get_UnknownConfirmationId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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

    /// <summary>
    /// Builds a real recipe with one operation, releases its version, creates an
    /// order against it and releases the order, so confirmations can be reported.
    /// </summary>
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
