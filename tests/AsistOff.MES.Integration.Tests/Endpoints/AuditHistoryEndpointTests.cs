using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Slice 2 (#246) history coverage: updating a Production Order appends exactly
/// one update row readable through the history browse; Machine create plus
/// delete yields create and delete rows with prior rows untouched; the browse
/// filters per entity in descending time order with paging; cross-tenant
/// browses return empty; failed primary writes leave no ghost history.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class AuditHistoryEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string OrdersUrl = "/api/production-orders";
    private const string MachinesUrl = "/api/machines";
    private const string ConfirmationsUrl = "/api/production-confirmations";
    private const string HistoryUrl = "/api/audit-events";

    [Fact]
    public async Task Browse_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(HistoryUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Browse_UnknownEntityName_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{HistoryUrl}?entityName=WorkCenter");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Browse_EntityIdWithoutEntityName_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{HistoryUrl}?entityId={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateProductionOrder_AppendsSingleUpdateRow_ReadableThroughHistory()
    {
        // Arrange — isolated tenant so the entity history is unambiguous.
        var (email, password) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var created = await CreateOrderAsync(client);

        // Act
        var update = await client.PutAsJsonAsync($"{OrdersUrl}/{created.Id}", new
        {
            id = created.Id,
            code = created.Code,
            productId = created.ProductId,
            recipeId = created.RecipeId,
            recipeVersionId = created.RecipeVersionId,
            plannedQuantity = created.PlannedQuantity,
            measureUnitId = (Guid?)null,
            priority = created.Priority,
            dueDate = (DateTime?)null,
            notes = "history probe",
            syncId = (string?)null
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — exactly the create row plus one update row, newest first.
        var browse = await client.GetAsync(
            $"{HistoryUrl}?entityName=ProductionOrder&entityId={created.Id}");
        browse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<AuditEventDto>>(browse);
        page.Items.Should().HaveCount(2);
        page.Items.Should().ContainSingle(x => x.Action == 0);
        page.Items.Should().ContainSingle(x => x.Action == 1);

        page.Items.Select(x => x.ChangedAt).Should().BeInDescendingOrder();
        page.Items.First().Action.Should().Be(1, "history reads newest-first");

        var updated = page.Items.Single(x => x.Action == 1);
        updated.EntityName.Should().Be("ProductionOrder");
        updated.EntityId.Should().Be(created.Id);
        updated.ActorId.Should().Be(created.CreatedBy, "same user created and updated the order");
        updated.ChangedAt.Should().BeOnOrAfter(page.Items.Single(x => x.Action == 0).ChangedAt);
        updated.Payload.Should().Contain("history probe");

        // Assert — the order detail history surface exposes the same rows.
        var history = await client.GetAsync($"{OrdersUrl}/{created.Id}/history");
        history.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await ReadAsync<PagedResponseDto<AuditEventDto>>(history);
        detail.Items.Should().HaveCount(2);
        detail.Items.Select(x => x.Id).Should().BeEquivalentTo(page.Items.Select(x => x.Id));
    }

    [Fact]
    public async Task CreateAndDeleteMachine_AppendsCreateAndDeleteRows_PriorRowsUntouched()
    {
        // Arrange
        var (email, password) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var created = await CreateMachineAsync(client);

        // Act
        var delete = await client.DeleteAsync($"{MachinesUrl}/{created.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert — create plus delete, newest first; no update of prior rows.
        var browse = await client.GetAsync($"{HistoryUrl}?entityName=Machine&entityId={created.Id}");
        browse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<AuditEventDto>>(browse);
        page.Items.Should().HaveCount(2);

        page.Items.Select(x => x.ChangedAt).Should().BeInDescendingOrder();
        page.Items.First().Action.Should().Be(2, "the delete is the newest row");

        page.Items.Should().ContainSingle(x => x.Action == 0);
        var deleted = page.Items.Should().ContainSingle(x => x.Action == 2).Subject;
        deleted.EntityName.Should().Be("Machine");
        deleted.EntityId.Should().Be(created.Id);
        deleted.ActorId.Should().Be(created.CreatedBy);
        page.Items.Should().NotContain(x => x.Action == 1);
    }

    [Fact]
    public async Task CreateAndDeleteConfirmation_AppendsCreateAndDeleteRows()
    {
        // Arrange
        var (email, password) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var order = await CreateReleasedOrderAsync(client);
        var create = await client.PostAsJsonAsync(ConfirmationsUrl, ConfirmPayload(order.Id));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var confirmation = await ReadAsync<ProductionConfirmationDto>(create);

        // Act
        var delete = await client.DeleteAsync($"{ConfirmationsUrl}/{confirmation.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert
        var browse = await client.GetAsync(
            $"{HistoryUrl}?entityName=ProductionConfirmation&entityId={confirmation.Id}");
        browse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<AuditEventDto>>(browse);
        page.Items.Should().HaveCount(2);
        page.Items.Should().ContainSingle(x => x.Action == 0);
        page.Items.First().Action.Should().Be(2);
    }

    [Fact]
    public async Task Browse_FilteredByEntity_ReturnsOnlyThatEntity_WithPaging()
    {
        // Arrange — two machines in one tenant; history must isolate per entity.
        var (email, password) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var first = await CreateMachineAsync(client);
        var second = await CreateMachineAsync(client);

        // Act
        var browse = await client.GetAsync($"{HistoryUrl}?entityName=Machine&entityId={first.Id}");
        browse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<AuditEventDto>>(browse);

        // Assert
        page.Items.Should().ContainSingle();
        page.Items.Single().EntityId.Should().Be(first.Id);
        page.Items.Should().NotContain(x => x.EntityId == second.Id);

        var paged = await client.GetAsync(
            $"{HistoryUrl}?entityName=Machine&entityId={first.Id}&pageNumber=1&pageSize=1");
        paged.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstPage = await ReadAsync<PagedResponseDto<AuditEventDto>>(paged);
        firstPage.TotalCount.Should().Be(1);
        firstPage.TotalPages.Should().Be(1);
        firstPage.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Browse_CrossTenant_ReturnsEmpty()
    {
        // Arrange — machine history lives in tenant A.
        var (emailA, passwordA) = await Fixture.CreateTenantAsync();
        using var clientA = await Fixture.CreateAuthenticatedClientAsync(emailA, passwordA);
        var machine = await CreateMachineAsync(clientA);

        var (emailB, passwordB) = await Fixture.CreateTenantAsync();
        using var clientB = await Fixture.CreateAuthenticatedClientAsync(emailB, passwordB);

        // Act — tenant B browses the same entity filter and the full history.
        var filtered = await clientB.GetAsync($"{HistoryUrl}?entityName=Machine&entityId={machine.Id}");
        var unfiltered = await clientB.GetAsync(HistoryUrl);

        // Assert — the global query filter hides tenant A rows entirely.
        filtered.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<PagedResponseDto<AuditEventDto>>(filtered)).Items.Should().BeEmpty();

        unfiltered.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<AuditEventDto>>(unfiltered);
        page.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task FailedPrimaryWrite_AppendsNoGhostHistory()
    {
        // Arrange — fresh tenant with no history yet.
        var (email, password) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);

        // Act — the create fails validation, so nothing is ever saved.
        var failed = await client.PostAsJsonAsync(OrdersUrl, new
        {
            code = "",
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
        failed.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Assert — no ghost history exists for the rolled back write.
        var browse = await client.GetAsync(HistoryUrl);
        browse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<AuditEventDto>>(browse);
        page.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
    }

    private async Task<ProductionOrderDto> CreateOrderAsync(
        HttpClient client, Guid? recipeId = null, Guid? recipeVersionId = null)
    {
        var response = await client.PostAsJsonAsync(OrdersUrl, new
        {
            code = $"PO-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            productId = Guid.NewGuid(),
            recipeId = recipeId ?? Guid.NewGuid(),
            recipeVersionId = recipeVersionId ?? Guid.NewGuid(),
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

    private async Task<MachineDto> CreateMachineAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(MachinesUrl, new
        {
            code = $"IT-{Guid.NewGuid():N}"[..12],
            name = "Work Center",
            description = (string?)null,
            isActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<MachineDto>(response);
    }

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

    private async Task<ProductionOrderDto> CreateReleasedOrderAsync(HttpClient client)
    {
        var recipe = await CreateRecipeAsync(client);
        var versionId = recipe.Versions.Single().Id;

        var operation = await client.PostAsJsonAsync("/api/operations", new
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
        operation.StatusCode.Should().Be(HttpStatusCode.OK);

        var releaseVersion = await client.PostAsync($"/api/recipe-versions/{versionId}/release", null);
        releaseVersion.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var order = await CreateOrderAsync(client, recipe.Id, versionId);

        var release = await client.PostAsync($"{OrdersUrl}/{order.Id}/release", null);
        release.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadAsync<ProductionOrderDto>(release);
    }

    private static async Task<RecipeDto> CreateRecipeAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/recipes", new
        {
            code = $"R-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            name = "Audit history recipe",
            description = (string?)null,
            isActive = true,
            primaryProductId = (Guid?)null,
            syncId = (string?)null
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<RecipeDto>(response);
    }
}
