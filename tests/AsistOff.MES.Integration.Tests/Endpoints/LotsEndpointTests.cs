using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>/api/lots</c>. They exercise the
/// full request pipeline: authentication, tenant resolution, MediatR handler,
/// EF Core persistence and the global exception handler - against a real
/// PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class LotsEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/lots";

    [Fact]
    public async Task Browse_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_ReturnsCreated_AndIsRetrievableById()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();
        var productId = Guid.NewGuid();
        var payload = NewPayload(code, productId);

        var createResponse = await client.PostAsJsonAsync(BaseUrl, payload);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<LotDto>(createResponse);
        created.Code.Should().Be(code);
        created.Id.Should().NotBeEmpty();
        created.ProductId.Should().Be(productId);
        created.Status.Should().Be(1); // Available

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadAsync<LotDto>(getResponse);
        fetched.Id.Should().Be(created.Id);
        fetched.Quantity.Should().Be(10m);
    }

    [Fact]
    public async Task Create_WithDuplicateCode_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();
        var payload = NewPayload(code);

        var first = await client.PostAsJsonAsync(BaseUrl, payload);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync(BaseUrl, payload);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_WithNegativeQuantity_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var payload = NewPayload(UniqueCode()) with { Quantity = -1m };

        var response = await client.PostAsJsonAsync(BaseUrl, payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithEmptyCode_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var payload = NewPayload("") ;

        var response = await client.PostAsJsonAsync(BaseUrl, payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithOverlongCode_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var payload = NewPayload(new string('L', 51));

        var response = await client.PostAsJsonAsync(BaseUrl, payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_WithRouteIdMismatch_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();
        var created = await ReadAsync<LotDto>(await client.PostAsJsonAsync(BaseUrl, NewPayload(code)));

        var response = await client.PutAsJsonAsync(
            $"{BaseUrl}/{Guid.NewGuid()}",
            NewPayload(code, created.ProductId, created.MeasureUnitId, 25m) with { Id = created.Id });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Lot_CreatedInAnotherTenant_IsNotVisible_AndSameCodeIsReusable()
    {
        // Arrange - create a lot as the seeded dev tenant.
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();
        var created = await ReadAsync<LotDto>(await devClient.PostAsJsonAsync(BaseUrl, NewPayload(code)));

        // Act - provision a second tenant.
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);

        // Assert - the other tenant cannot see the lot by id or by code,
        // but the same code is reusable in its own tenant scope.
        (await otherTenantClient.GetAsync($"{BaseUrl}/{created.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await otherTenantClient.GetAsync($"{BaseUrl}/by-code/{code}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var reuse = await otherTenantClient.PostAsJsonAsync(BaseUrl, NewPayload(code));

        reuse.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Get_WithUnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetByCode_ReturnsLot_AndUnknownCodeReturns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();
        var create = await client.PostAsJsonAsync(BaseUrl, NewPayload(code));
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var getResponse = await client.GetAsync($"{BaseUrl}/by-code/{code}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadAsync<LotDto>(getResponse);
        fetched.Code.Should().Be(code);

        var missing = await client.GetAsync($"{BaseUrl}/by-code/NOPE-{Guid.NewGuid():N}");

        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Browse_WithCodeFilter_ReturnsMatchingItem()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();
        await client.PostAsJsonAsync(BaseUrl, NewPayload(code));

        var response = await client.GetAsync($"{BaseUrl}?code={code}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<LotDto>>(response);
        page.Items.Should().ContainSingle(item => item.Code == code);
    }

    [Fact]
    public async Task Browse_WithSearch_ReturnsAtMost20MatchesOrderedByCode()
    {
        // Arrange - 25 lots sharing one code prefix.
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = $"SCH-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        for (var i = 0; i < 25; i++)
        {
            var create = await client.PostAsJsonAsync(BaseUrl, NewPayload($"{tag}-{i:000}"));
            create.EnsureSuccessStatusCode();
        }

        // Act - the typeahead asks for a huge page; the server caps it.
        var response = await client.GetAsync($"{BaseUrl}?search={tag}&pageNumber=1&pageSize=500");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<LotDto>>(response);
        page.Items.Should().HaveCount(20);
        page.Items.Select(l => l.Code).Should().BeInAscendingOrder();
        page.Items.Should().OnlyContain(l => l.Code.Contains(tag));
    }

    [Fact]
    public async Task ChangeStatus_LegalTransition_AppliesAndIllegalTransitionReturns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();
        var create = await client.PostAsJsonAsync(BaseUrl, NewPayload(code));
        var created = await ReadAsync<LotDto>(create);

        // Available -> OnHold is legal.
        var hold = await client.PostAsJsonAsync($"{BaseUrl}/{created.Id}/status", new { status = 2 });

        hold.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var held = await ReadAsync<LotDto>(await client.GetAsync($"{BaseUrl}/{created.Id}"));
        held.Status.Should().Be(2);

        // OnHold -> Scrapped is legal.
        var scrap = await client.PostAsJsonAsync($"{BaseUrl}/{created.Id}/status", new { status = 4 });

        scrap.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Scrapped -> Available is illegal (terminal state).
        var illegal = await client.PostAsJsonAsync($"{BaseUrl}/{created.Id}/status", new { status = 1 });

        illegal.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_OnlyAllowedForAvailableLots()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        // Available lot can be deleted.
        var availableCode = UniqueCode();
        var availableCreate = await client.PostAsJsonAsync(BaseUrl, NewPayload(availableCode));
        var available = await ReadAsync<LotDto>(availableCreate);

        var deleteAvailable = await client.DeleteAsync($"{BaseUrl}/{available.Id}");

        deleteAvailable.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"{BaseUrl}/{available.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // OnHold lot cannot be deleted.
        var heldCode = UniqueCode();
        var heldCreate = await client.PostAsJsonAsync(BaseUrl, NewPayload(heldCode));
        var held = await ReadAsync<LotDto>(heldCreate);
        (await client.PostAsJsonAsync($"{BaseUrl}/{held.Id}/status", new { status = 2 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var deleteHeld = await client.DeleteAsync($"{BaseUrl}/{held.Id}");

        deleteHeld.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_EditsMetadata()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();
        var create = await client.PostAsJsonAsync(BaseUrl, NewPayload(code));
        var created = await ReadAsync<LotDto>(create);
        var newProductId = Guid.NewGuid();

        var update = await client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", NewPayload(code, newProductId, created.MeasureUnitId, 25m) with { Id = created.Id });

        update.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var fetched = await ReadAsync<LotDto>(await client.GetAsync($"{BaseUrl}/{created.Id}"));
        fetched.Quantity.Should().Be(25m);
        fetched.ProductId.Should().Be(newProductId);
    }

    private static string UniqueCode() => $"LOT-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    private sealed record LotPayload(
        Guid Id,
        string Code,
        Guid ProductId,
        Guid MeasureUnitId,
        decimal Quantity,
        string? SupplierLotNumber,
        DateTime? ProducedAt,
        DateTime? ExpiryDate,
        string? Notes);

    private static LotPayload NewPayload(
        string code,
        Guid? productId = null,
        Guid? measureUnitId = null,
        decimal quantity = 10m) =>
        new(Guid.Empty, code, productId ?? Guid.NewGuid(), measureUnitId ?? Guid.NewGuid(),
            quantity, null, null, null, null);
}
