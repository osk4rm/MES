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
/// Endpoint-scoped integration tests for <c>/api/production-confirmations</c>.
/// They exercise the full request pipeline: authentication, tenant resolution,
/// MediatR handlers, EF Core persistence and the global exception handler -
/// against a real PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ProductionConfirmationsEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/production-confirmations";
    private const string OrdersUrl = "/api/production-orders";

    [Fact]
    public async Task Browse_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_ReleasedOrder_ReturnsCreated_TransitionsOrderToInProgress()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);

        var createResponse = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(order.Id));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ProductionConfirmationDto>(createResponse);
        created.ProductionOrderId.Should().Be(order.Id);
        created.GoodQuantity.Should().Be(10m);
        created.ScrapQuantity.Should().Be(2m);

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var orderResponse = await client.GetAsync($"{OrdersUrl}/{order.Id}");
        var fetchedOrder = await ReadAsync<ProductionOrderDto>(orderResponse);
        fetchedOrder.Status.Should().Be(3); // InProgress
    }

    [Fact]
    public async Task Browse_ByProductionOrderId_ReturnsCallerTenantRowsOnly()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);
        var machineId = Guid.NewGuid();
        var create = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(order.Id, machineId: machineId));
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.GetAsync($"{BaseUrl}?productionOrderId={order.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<ProductionConfirmationDto>>(response);
        page.Items.Should().ContainSingle(item => item.ProductionOrderId == order.Id && item.MachineId == machineId);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);

        var crossTenantResponse = await otherTenantClient.GetAsync($"{BaseUrl}?productionOrderId={order.Id}");

        crossTenantResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var crossTenantPage = await ReadAsync<PagedResponseDto<ProductionConfirmationDto>>(crossTenantResponse);
        crossTenantPage.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_PlannedOrder_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var planned = await CreateOrderAsync(client);

        var response = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(planned.Id));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_CrossTenantOrder_Returns404()
    {
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(devClient);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);

        var response = await otherTenantClient.PostAsJsonAsync(BaseUrl, ConfirmPayload(order.Id));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_UnknownOrder_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_ZeroTotalQuantity_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);

        var response = await client.PostAsJsonAsync(
            BaseUrl, ConfirmPayload(order.Id, goodQuantity: 0m, scrapQuantity: 0m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_NegativeQuantity_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);

        var response = await client.PostAsJsonAsync(
            BaseUrl, ConfirmPayload(order.Id, goodQuantity: -1m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_FutureReportedAt_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);

        var response = await client.PostAsJsonAsync(
            BaseUrl, ConfirmPayload(order.Id, reportedAt: DateTime.UtcNow.AddHours(2)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_ReportedAtBeforeRelease_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);

        var response = await client.PostAsJsonAsync(
            BaseUrl, ConfirmPayload(order.Id, reportedAt: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_OpenOrder_Returns204()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);
        var create = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(order.Id));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ProductionConfirmationDto>(create);

        var deleteResponse = await client.DeleteAsync($"{BaseUrl}/{created.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Browse_FiltersByMachineId()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var order = await CreateReleasedOrderAsync(client);
        var machineA = Guid.NewGuid();
        var machineB = Guid.NewGuid();
        var createA = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(order.Id, machineId: machineA));
        createA.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdA = await ReadAsync<ProductionConfirmationDto>(createA);
        var createB = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(order.Id, machineId: machineB));
        createB.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdB = await ReadAsync<ProductionConfirmationDto>(createB);

        var response = await client.GetAsync($"{BaseUrl}?machineId={machineA}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<ProductionConfirmationDto>>(response);
        page.Items.Should().ContainSingle(item => item.Id == createdA.Id);
        page.Items.Should().NotContain(item => item.Id == createdB.Id);
    }

    [Fact]
    public async Task Browse_FiltersByReportedAtRange()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var before = DateTime.UtcNow;
        var order = await CreateReleasedOrderAsync(client);
        var create = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(order.Id));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ProductionConfirmationDto>(create);

        var inRange = await client.GetAsync(
            $"{BaseUrl}?from={Uri.EscapeDataString(before.AddHours(-1).ToString("O"))}&to={Uri.EscapeDataString(before.AddHours(1).ToString("O"))}");

        inRange.StatusCode.Should().Be(HttpStatusCode.OK);
        var inRangePage = await ReadAsync<PagedResponseDto<ProductionConfirmationDto>>(inRange);
        inRangePage.Items.Should().Contain(item => item.Id == created.Id);

        var afterRange = await client.GetAsync(
            $"{BaseUrl}?from={Uri.EscapeDataString(before.AddHours(2).ToString("O"))}");

        afterRange.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterRangePage = await ReadAsync<PagedResponseDto<ProductionConfirmationDto>>(afterRange);
        afterRangePage.Items.Should().NotContain(item => item.Id == created.Id);

        var beforeRange = await client.GetAsync(
            $"{BaseUrl}?to={Uri.EscapeDataString(before.AddHours(-2).ToString("O"))}");

        beforeRange.StatusCode.Should().Be(HttpStatusCode.OK);
        var beforeRangePage = await ReadAsync<PagedResponseDto<ProductionConfirmationDto>>(beforeRange);
        beforeRangePage.Items.Should().NotContain(item => item.Id == created.Id);
    }

    [Fact]
    public async Task Browse_SupportsPaging()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var order = await CreateReleasedOrderAsync(client);
        for (var i = 0; i < 3; i++)
        {
            var create = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(order.Id));
            create.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var firstPage = await client.GetAsync($"{BaseUrl}?pageNumber=1&pageSize=2");

        firstPage.StatusCode.Should().Be(HttpStatusCode.OK);
        var first = await ReadAsync<PagedResponseDto<ProductionConfirmationDto>>(firstPage);
        first.TotalCount.Should().Be(3);
        first.TotalPages.Should().Be(2);
        first.Items.Should().HaveCount(2);

        var secondPage = await client.GetAsync($"{BaseUrl}?pageNumber=2&pageSize=2");

        secondPage.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await ReadAsync<PagedResponseDto<ProductionConfirmationDto>>(secondPage);
        second.Items.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(ProductionOrderStatus.Completed)]
    [InlineData(ProductionOrderStatus.Closed)]
    public async Task Create_FinalOrder_Returns409(ProductionOrderStatus status)
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);
        await SetOrderStatusAsync(order.Id, status);

        var response = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(order.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData(ProductionOrderStatus.Completed)]
    [InlineData(ProductionOrderStatus.Closed)]
    public async Task Delete_FinalOrder_Returns409(ProductionOrderStatus status)
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var order = await CreateReleasedOrderAsync(client);
        var create = await client.PostAsJsonAsync(BaseUrl, ConfirmPayload(order.Id));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ProductionConfirmationDto>(create);
        await SetOrderStatusAsync(order.Id, status);

        var response = await client.DeleteAsync($"{BaseUrl}/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private static object ConfirmPayload(
        Guid productionOrderId,
        Guid? machineId = null,
        decimal goodQuantity = 10m,
        decimal scrapQuantity = 2m,
        DateTime? reportedAt = null) => new
        {
            productionOrderId,
            machineId = machineId ?? Guid.NewGuid(),
            reportedByOperatorId = (Guid?)null,
            reportedAt = reportedAt ?? DateTime.UtcNow,
            goodQuantity,
            scrapQuantity,
            notes = (string?)null
        };

    private static string UniqueCode() => $"PO-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    /// <summary>
    /// Moves an order straight to a final status via the database. There is no
    /// Complete/Close endpoint in this slice, so the final-state create/delete
    /// rejections are otherwise unreachable over HTTP.
    /// </summary>
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
            code = UniqueCode(),
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

    /// <summary>
    /// Builds a real recipe with one operation, releases its version, creates an
    /// order against it and releases the order.
    /// </summary>
    private async Task<ProductionOrderDto> CreateReleasedOrderAsync(HttpClient client)
    {
        var recipe = await CreateRecipeAsync(client);
        var versionId = recipe.Versions.Single().Id;

        var operationResponse = await client.PostAsJsonAsync("/api/operations", new
        {
            versionId,
            code = $"OP-{Guid.NewGuid():N}"[..8],
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

    private static async Task<RecipeDto> CreateRecipeAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/recipes", new
        {
            code = $"R-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            name = "Integration recipe",
            description = (string?)null,
            isActive = true,
            primaryProductId = (Guid?)null,
            syncId = (string?)null
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<RecipeDto>(response);
    }
}
