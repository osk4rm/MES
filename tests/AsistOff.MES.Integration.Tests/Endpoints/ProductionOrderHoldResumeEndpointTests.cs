using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>POST /api/production-orders/{id}/hold</c>
/// and <c>/resume</c> (issue #398). They exercise the full request pipeline:
/// authentication, tenant resolution, MediatR handlers, EF Core persistence and
/// the global exception handler - against a real PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ProductionOrderHoldResumeEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/production-orders";
    private const string ConfirmationsUrl = "/api/production-confirmations";

    [Fact]
    public async Task Hold_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var hold = await client.PostAsync($"{BaseUrl}/{Guid.NewGuid()}/hold", null);
        var resume = await client.PostAsync($"{BaseUrl}/{Guid.NewGuid()}/resume", null);

        hold.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        resume.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Hold_ReleasedOrder_Returns200WithOnHoldStatusAndHoldState()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var released = await CreateReleasedOrderAsync(client);

        var response = await client.PostAsync(
            $"{BaseUrl}/{released.Id}/hold?holdReason={Uri.EscapeDataString("MATERIAL-SHORTAGE")}", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var held = await ReadAsync<ProductionOrderDto>(response);
        held.Status.Should().Be(6); // OnHold
        held.StatusBeforeHold.Should().Be(2); // Released
        held.HoldReason.Should().Be("MATERIAL-SHORTAGE");
        held.HeldAtUtc.Should().NotBeNull();

        var getResponse = await client.GetAsync($"{BaseUrl}/{released.Id}");
        var fetched = await ReadAsync<ProductionOrderDto>(getResponse);
        fetched.Status.Should().Be(6);
        fetched.HoldReason.Should().Be("MATERIAL-SHORTAGE");
        fetched.HeldAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Hold_InProgressOrder_ConfirmBlocked_ResumeRestoresInProgress_ConfirmSucceeds()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var released = await CreateReleasedOrderAsync(client);
        var first = await client.PostAsJsonAsync(ConfirmationsUrl, ConfirmPayload(released.Id));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var hold = await client.PostAsync($"{BaseUrl}/{released.Id}/hold?holdReason={Uri.EscapeDataString("QUALITY")}", null);

        hold.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<ProductionOrderDto>(hold)).Status.Should().Be(6);

        var blocked = await client.PostAsJsonAsync(ConfirmationsUrl, ConfirmPayload(released.Id));

        blocked.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var resume = await client.PostAsync($"{BaseUrl}/{released.Id}/resume", null);

        resume.StatusCode.Should().Be(HttpStatusCode.OK);
        var resumed = await ReadAsync<ProductionOrderDto>(resume);
        resumed.Status.Should().Be(3); // InProgress restored
        resumed.HeldAtUtc.Should().BeNull();
        resumed.HoldReason.Should().BeNull();
        resumed.StatusBeforeHold.Should().BeNull();

        var retry = await client.PostAsJsonAsync(ConfirmationsUrl, ConfirmPayload(released.Id));

        retry.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Complete_AndClose_AndRelease_OnHoldOrder_Return400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var released = await CreateReleasedOrderAsync(client);
        var first = await client.PostAsJsonAsync(ConfirmationsUrl, ConfirmPayload(released.Id));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var hold = await client.PostAsync($"{BaseUrl}/{released.Id}/hold", null);
        hold.StatusCode.Should().Be(HttpStatusCode.OK);

        var complete = await client.PostAsync($"{BaseUrl}/{released.Id}/complete", null);
        var close = await client.PostAsync($"{BaseUrl}/{released.Id}/close", null);
        var release = await client.PostAsync($"{BaseUrl}/{released.Id}/release", null);

        complete.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        close.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        release.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Hold_PlannedOrder_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var planned = await CreateOrderAsync(client);

        var response = await client.PostAsync($"{BaseUrl}/{planned.Id}/hold", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Hold_SecondTime_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var released = await CreateReleasedOrderAsync(client);

        var first = await client.PostAsync($"{BaseUrl}/{released.Id}/hold", null);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PostAsync($"{BaseUrl}/{released.Id}/hold", null);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Resume_NotHeldOrder_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var released = await CreateReleasedOrderAsync(client);

        var response = await client.PostAsync($"{BaseUrl}/{released.Id}/resume", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Hold_WithStaleToken_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var released = await CreateReleasedOrderAsync(client);
        var staleToken = released.ConcurrencyToken;

        var first = await client.PostAsync($"{BaseUrl}/{released.Id}/hold?concurrencyToken={staleToken}", null);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PostAsync($"{BaseUrl}/{released.Id}/resume?concurrencyToken={staleToken}", null);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Hold_UnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var hold = await client.PostAsync($"{BaseUrl}/{Guid.NewGuid()}/hold", null);
        var resume = await client.PostAsync($"{BaseUrl}/{Guid.NewGuid()}/resume", null);

        hold.StatusCode.Should().Be(HttpStatusCode.NotFound);
        resume.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Hold_OrderFromAnotherTenant_Returns404()
    {
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var released = await CreateReleasedOrderAsync(devClient);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);

        var hold = await otherTenantClient.PostAsync($"{BaseUrl}/{released.Id}/hold", null);
        var resume = await otherTenantClient.PostAsync($"{BaseUrl}/{released.Id}/resume", null);

        hold.StatusCode.Should().Be(HttpStatusCode.NotFound);
        resume.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DispatchBoard_ShowsHeldOrderAsBlocked()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = UniqueTag();
        // Due inside the window so the row sorts with the dated orders, ahead
        // of the shared tenant's null-due-date rows under the 200-row cap.
        var released = await CreateReleasedOrderAsync(
            client, tag, new DateTime(2027, 3, 11, 12, 0, 0, DateTimeKind.Utc));

        var hold = await client.PostAsync($"{BaseUrl}/{released.Id}/hold", null);
        hold.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.GetAsync("/api/schedule/dispatch?from=2027-03-10&to=2027-03-12");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var board = await ReadAsync<DispatchBoardDto>(response);
        var row = board.Orders.Single(o => o.Code == released.Code);
        row.Status.Should().Be(6);
        row.IsBlocked.Should().BeTrue();
    }

    [Fact]
    public async Task Gantt_ShowsHeldOrderBarsAsBlocked()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = UniqueTag();
        // A dated order anchors its bars backward from the due date, so the
        // window below covers them and the row sorts ahead of null-due rows.
        var released = await CreateReleasedOrderAsync(
            client, tag, DateTime.UtcNow.AddDays(1));

        var hold = await client.PostAsync($"{BaseUrl}/{released.Id}/hold", null);
        hold.StatusCode.Should().Be(HttpStatusCode.OK);

        // The dated order anchors its bars backward from the due date, so a
        // window covering today through next week contains them.
        var from = DateOnly.FromDateTime(DateTime.UtcNow);
        var to = from.AddDays(6);
        var response = await client.GetAsync($"/api/schedule/gantt?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var schedule = await ReadAsync<GanttScheduleDto>(response);
        var bars = schedule.Groups.SelectMany(g => g.Bars).Where(b => b.ProductionOrderCode == released.Code).ToList();
        bars.Should().NotBeEmpty();
        bars.Should().OnlyContain(b => b.IsBlocked);
    }

    private static string UniqueTag() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static string UniqueCode() => $"PO-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    private static object ConfirmPayload(Guid productionOrderId) => new
    {
        productionOrderId,
        machineId = Guid.NewGuid(),
        reportedByOperatorId = (Guid?)null,
        reportedAt = DateTime.UtcNow,
        goodQuantity = 10m,
        scrapQuantity = 0m,
        notes = (string?)null
    };

    private async Task<ProductionOrderDto> CreateOrderAsync(
        HttpClient client, Guid? recipeId = null, Guid? recipeVersionId = null, string? tag = null, DateTime? dueDate = null)
    {
        var payload = new
        {
            code = tag is null ? UniqueCode() : $"PO-{tag}-{Guid.NewGuid():N}".ToUpperInvariant(),
            productId = Guid.NewGuid(),
            recipeId = recipeId ?? Guid.NewGuid(),
            recipeVersionId = recipeVersionId ?? Guid.NewGuid(),
            plannedQuantity = 100m,
            measureUnitId = (Guid?)null,
            priority = 0,
            dueDate,
            notes = (string?)null,
            syncId = (string?)null
        };

        var response = await client.PostAsJsonAsync(BaseUrl, payload);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<ProductionOrderDto>(response);
    }

    /// <summary>
    /// Builds a real recipe with one operation, releases its version, creates an
    /// order against it and releases the order.
    /// </summary>
    private async Task<ProductionOrderDto> CreateReleasedOrderAsync(HttpClient client, string? tag = null, DateTime? dueDate = null)
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

        var order = await CreateOrderAsync(client, recipe.Id, versionId, tag, dueDate);

        var releaseOrderResponse = await client.PostAsync($"{BaseUrl}/{order.Id}/release", null);
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
