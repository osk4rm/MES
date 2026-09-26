using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Hardening tests for issue #274: Lot and Product <c>Search</c> typeahead
/// returns at most 20 matches ordered by code, with server-side MaxPageSize
/// enforcement (400 on over-max pages, 401 without a token).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class LotProductSearchEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Lots_Search_ReturnsCappedMatches_OrderedByCode()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = UniqueTag();
        for (var i = 25; i >= 0; i--)
            await CreateLotAsync(client, $"SCH-{tag}-{i:000}");

        var response = await client.GetAsync($"/api/lots?search=SCH-{tag}&pageNumber=1&pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<LotDto>>(response);
        page.Items.Should().HaveCountLessThanOrEqualTo(20);
        page.Items.Select(l => l.Code).Should().BeInAscendingOrder();
        page.Items.Should().OnlyContain(l => l.Code.Contains($"SCH-{tag}"));
    }

    [Fact]
    public async Task Lots_Browse_PageSizeAboveMax_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/lots?pageNumber=1&pageSize=201");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Lots_Browse_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync("/api/lots?search=SCH");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Products_Search_ReturnsCappedMatches_OrderedByCode()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = UniqueTag();
        for (var i = 25; i >= 0; i--)
            await CreateProductAsync(client, $"SCH-{tag}-{i:000}");

        var response = await client.GetAsync($"/api/products?search=SCH-{tag}&pageNumber=1&pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<ProductDto>>(response);
        page.Items.Should().HaveCountLessThanOrEqualTo(20);
        page.Items.Select(p => p.Code).Should().BeInAscendingOrder();
        page.Items.Should().OnlyContain(p => p.Code.Contains($"SCH-{tag}"));
    }

    [Fact]
    public async Task Products_Browse_PageSizeAboveMax_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/products?pageNumber=1&pageSize=101");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Products_Browse_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync("/api/products?search=SCH");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string UniqueTag() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static async Task CreateLotAsync(HttpClient client, string code)
    {
        var response = await client.PostAsJsonAsync("/api/lots", new
        {
            id = Guid.Empty,
            code,
            productId = Guid.NewGuid(),
            measureUnitId = Guid.NewGuid(),
            quantity = 10m,
            supplierLotNumber = (string?)null,
            producedAt = (DateTime?)null,
            expiryDate = (DateTime?)null,
            notes = (string?)null
        });
        response.EnsureSuccessStatusCode();
    }

    private static async Task CreateProductAsync(HttpClient client, string code)
    {
        var response = await client.PostAsJsonAsync("/api/products", new
        {
            code,
            name = $"Search product {code}",
            description = (string?)null,
            ean = (string?)null,
            barcode = (string?)null,
            scanBy = 1,
            isActive = true,
            productGroupId = (Guid?)null,
            syncId = (string?)null
        });
        response.EnsureSuccessStatusCode();
    }

    private sealed record ProductDto(Guid Id, string? SyncId, string Code, string Name);
}
