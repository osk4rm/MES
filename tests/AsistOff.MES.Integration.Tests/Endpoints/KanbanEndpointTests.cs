using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>/api/kanban</c>. They exercise the
/// full request pipeline: authentication, tenant resolution, MediatR handler,
/// EF Core persistence and the global exception handler - against a real
/// PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class KanbanEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string LoopsUrl = "/api/kanban/loops";
    private static string CardsUrl(Guid loopId) => $"{LoopsUrl}/{loopId}/cards";
    private static string TransitionUrl(Guid cardId, string action) => $"/api/kanban/cards/{cardId}/{action}";

    [Fact]
    public async Task BrowseLoops_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(LoopsUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BrowseCards_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(CardsUrl(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateLoop_ReturnsCreated_AndIsRetrievableById()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();
        var productId = Guid.NewGuid();

        var createResponse = await client.PostAsJsonAsync(LoopsUrl, NewLoopPayload(code, productId));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<KanbanLoopDto>(createResponse);
        created.Code.Should().Be(code);
        created.Id.Should().NotBeEmpty();
        created.ProductId.Should().Be(productId);
        created.IsActive.Should().BeTrue();
        created.CardQuantity.Should().Be(10m);

        var getResponse = await client.GetAsync($"{LoopsUrl}/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadAsync<KanbanLoopDto>(getResponse);
        fetched.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task CreateLoop_WithDuplicateCode_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();

        var first = await client.PostAsJsonAsync(LoopsUrl, NewLoopPayload(code));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync(LoopsUrl, NewLoopPayload(code));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateLoop_WithZeroQuantity_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(LoopsUrl, NewLoopPayload(UniqueCode()) with { CardQuantity = 0m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task CreateLoop_WithCirculationOutOfBounds_Returns400(int circulation)
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(LoopsUrl, NewLoopPayload(UniqueCode()) with { CardsInCirculation = circulation });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateCard_ReturnsCreatedWithFullStatus_AndBrowseByLoopIdReturnsIt()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var loop = await ReadAsync<KanbanLoopDto>(
            await client.PostAsJsonAsync(LoopsUrl, NewLoopPayload(UniqueCode())));

        var createResponse = await client.PostAsJsonAsync(CardsUrl(loop.Id), new { cardNumber = (string?)null, notes = (string?)null });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<KanbanCardDto>(createResponse);
        created.LoopId.Should().Be(loop.Id);
        created.Status.Should().Be(1); // Full
        created.CardNumber.Should().NotBeNullOrWhiteSpace();

        var browseResponse = await client.GetAsync(CardsUrl(loop.Id));

        browseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<KanbanCardDto>>(browseResponse);
        page.Items.Should().ContainSingle(item => item.Id == created.Id);
    }

    [Fact]
    public async Task CreateCard_WithCrossTenantLoopId_Returns404()
    {
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var loop = await ReadAsync<KanbanLoopDto>(
            await devClient.PostAsJsonAsync(LoopsUrl, NewLoopPayload(UniqueCode())));

        var (otherEmail, otherPassword) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(otherEmail, otherPassword);

        var response = await otherTenantClient.PostAsJsonAsync(
            CardsUrl(loop.Id), new { cardNumber = "KB-X-01", notes = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateCard_WithUnknownLoopId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            CardsUrl(Guid.NewGuid()), new { cardNumber = "KB-X-01", notes = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Loop_CreatedInAnotherTenant_IsNotVisible_AndSameCodeIsReusable()
    {
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();
        var created = await ReadAsync<KanbanLoopDto>(
            await devClient.PostAsJsonAsync(LoopsUrl, NewLoopPayload(code)));

        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);

        (await otherTenantClient.GetAsync($"{LoopsUrl}/{created.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var reuse = await otherTenantClient.PostAsJsonAsync(LoopsUrl, NewLoopPayload(code));

        reuse.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task BrowseLoops_WithCodeFilter_ReturnsMatchingItem()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();
        await client.PostAsJsonAsync(LoopsUrl, NewLoopPayload(code));

        var response = await client.GetAsync($"{LoopsUrl}?code={code}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<KanbanLoopDto>>(response);
        page.Items.Should().ContainSingle(item => item.Code == code);
    }

    [Fact]
    public async Task DeleteLoop_CascadesItsCards_AndUnknownIdReturns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var loop = await ReadAsync<KanbanLoopDto>(
            await client.PostAsJsonAsync(LoopsUrl, NewLoopPayload(UniqueCode())));
        var card = await ReadAsync<KanbanCardDto>(
            await client.PostAsJsonAsync(CardsUrl(loop.Id), new { cardNumber = "KB-C-01", notes = (string?)null }));

        var delete = await client.DeleteAsync($"{LoopsUrl}/{loop.Id}");

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"{LoopsUrl}/{loop.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"{CardsUrl(loop.Id)}/{card.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await client.DeleteAsync($"{LoopsUrl}/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateLoop_WithZeroQuantity_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var loop = await ReadAsync<KanbanLoopDto>(
            await client.PostAsJsonAsync(LoopsUrl, NewLoopPayload(UniqueCode())));

        var response = await client.PutAsJsonAsync(
            $"{LoopsUrl}/{loop.Id}",
            new { cardQuantity = 0m, cardsInCirculation = 2, isActive = true, notes = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("consume")]
    [InlineData("order")]
    [InlineData("replenish")]
    public async Task Transition_WithoutToken_Returns401(string action)
    {
        using var client = Fixture.CreateClient();

        var response = await client.PostAsync(TransitionUrl(Guid.NewGuid(), action), null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Transition_HappyPath_RoundTripsFullToEmptyToOrderedToFull()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var card = await CreateCardAsync(client);

        var consume = await client.PostAsync(TransitionUrl(card.Id, "consume"), null);

        consume.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<KanbanCardDto>(consume)).Status.Should().Be(2); // Empty

        var order = await client.PostAsync(TransitionUrl(card.Id, "order"), null);

        order.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<KanbanCardDto>(order)).Status.Should().Be(3); // Ordered

        var replenish = await client.PostAsync(TransitionUrl(card.Id, "replenish"), null);

        replenish.StatusCode.Should().Be(HttpStatusCode.OK);
        var replenished = await ReadAsync<KanbanCardDto>(replenish);
        replenished.Status.Should().Be(1); // Full
        replenished.Id.Should().Be(card.Id);
    }

    [Fact]
    public async Task Transition_FromWrongStatus_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var card = await CreateCardAsync(client);

        // Fresh cards are Full: ordering or replenishing them is illegal.
        (await client.PostAsync(TransitionUrl(card.Id, "order"), null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.PostAsync(TransitionUrl(card.Id, "replenish"), null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        // After consuming, a repeat consume is illegal.
        (await client.PostAsync(TransitionUrl(card.Id, "consume"), null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync(TransitionUrl(card.Id, "consume"), null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Transition_WithCrossTenantCardId_Returns404()
    {
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var card = await CreateCardAsync(devClient);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);

        (await otherTenantClient.PostAsync(TransitionUrl(card.Id, "consume"), null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await otherTenantClient.PostAsync(TransitionUrl(card.Id, "order"), null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await otherTenantClient.PostAsync(TransitionUrl(card.Id, "replenish"), null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Transition_WithUnknownCardId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var unknownId = Guid.NewGuid();

        (await client.PostAsync(TransitionUrl(unknownId, "consume"), null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsync(TransitionUrl(unknownId, "order"), null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsync(TransitionUrl(unknownId, "replenish"), null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Replenish_AgainstInactiveLoop_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var loop = await ReadAsync<KanbanLoopDto>(
            await client.PostAsJsonAsync(LoopsUrl, NewLoopPayload(UniqueCode())));
        var card = await CreateCardAsync(client, loop.Id);

        (await client.PostAsync(TransitionUrl(card.Id, "consume"), null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync(TransitionUrl(card.Id, "order"), null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var deactivate = await client.PutAsJsonAsync(
            $"{LoopsUrl}/{loop.Id}",
            new { cardQuantity = 10m, cardsInCirculation = 2, isActive = false, notes = (string?)null });

        deactivate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.PostAsync(TransitionUrl(card.Id, "replenish"), null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Order_BeyondCirculationLimit_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var loop = await ReadAsync<KanbanLoopDto>(
            await client.PostAsJsonAsync(LoopsUrl, NewLoopPayload(UniqueCode()) with { CardsInCirculation = 1 }));
        var first = await CreateCardAsync(client, loop.Id);
        var second = await CreateCardAsync(client, loop.Id);

        (await client.PostAsync(TransitionUrl(first.Id, "consume"), null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync(TransitionUrl(first.Id, "order"), null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.PostAsync(TransitionUrl(second.Id, "consume"), null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync(TransitionUrl(second.Id, "order"), null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task BrowseCards_ByStatus_ReturnsOnlyMatchingCallerTenantRows()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var loop = await ReadAsync<KanbanLoopDto>(
            await client.PostAsJsonAsync(LoopsUrl, NewLoopPayload(UniqueCode())));
        var full = await CreateCardAsync(client, loop.Id);
        var emptied = await CreateCardAsync(client, loop.Id);
        (await client.PostAsync(TransitionUrl(emptied.Id, "consume"), null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var emptyPage = await ReadAsync<PagedResponseDto<KanbanCardDto>>(
            await client.GetAsync($"{CardsUrl(loop.Id)}?status=Empty"));

        emptyPage.Items.Should().ContainSingle(item => item.Id == emptied.Id);
        emptyPage.Items.Should().NotContain(item => item.Id == full.Id);

        var setPage = await ReadAsync<PagedResponseDto<KanbanCardDto>>(
            await client.GetAsync($"{CardsUrl(loop.Id)}?statuses=Empty&statuses=Ordered"));

        setPage.Items.Should().ContainSingle(item => item.Id == emptied.Id);

        // Another tenant's Full cards must never leak into this browse.
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var otherPage = await ReadAsync<PagedResponseDto<KanbanCardDto>>(
            await otherTenantClient.GetAsync($"{CardsUrl(loop.Id)}?status=Full"));

        otherPage.Items.Should().BeEmpty();
    }

    private static string UniqueCode() => $"KB-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    private static async Task<KanbanCardDto> CreateCardAsync(HttpClient client, Guid? loopId = null)
    {
        var targetLoopId = loopId ?? (await ReadAsync<KanbanLoopDto>(
            await client.PostAsJsonAsync(LoopsUrl, NewLoopPayload(UniqueCode())))).Id;

        var createResponse = await client.PostAsJsonAsync(
            CardsUrl(targetLoopId), new { cardNumber = (string?)null, notes = (string?)null });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<KanbanCardDto>(createResponse);
    }

    private sealed record LoopPayload(
        string Code,
        Guid ProductId,
        Guid ConsumingMachineId,
        Guid SupplyingWarehouseId,
        decimal CardQuantity,
        int CardsInCirculation,
        string? Notes);

    private static LoopPayload NewLoopPayload(string code, Guid? productId = null) =>
        new(code, productId ?? Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m, 2, null);
}
