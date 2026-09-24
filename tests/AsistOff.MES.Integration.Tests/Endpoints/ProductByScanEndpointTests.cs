using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint tests for <c>GET /api/products/by-scan</c> covering issue #166:
/// exact Code/Ean/Barcode resolution to a single active product.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ProductByScanEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/products";
    private const string ByScanUrl = "/api/products/by-scan";

    [Fact]
    public async Task GetByScan_WithoutToken_Returns401()
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.GetAsync($"{ByScanUrl}?value=ANYTHING");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetByScan_WithoutValue_Returns400()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync(ByScanUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetByScan_WithEmptyValue_Returns400()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync($"{ByScanUrl}?value=%20%20");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetByScan_WithExactCode_Returns200WithProduct()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var created = await CreateProductAsync(client);

        // Act
        var response = await client.GetAsync($"{ByScanUrl}?value={created.Code}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var found = await ReadAsync<ProductDto>(response);
        found.Id.Should().Be(created.Id);
        found.Code.Should().Be(created.Code);
        found.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetByScan_WithExactEan_Returns200WithProduct()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var created = await CreateProductAsync(client);

        // Act
        var response = await client.GetAsync($"{ByScanUrl}?value={created.Ean}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var found = await ReadAsync<ProductDto>(response);
        found.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GetByScan_WithExactBarcode_Returns200WithProduct()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var created = await CreateProductAsync(client);

        // Act
        var response = await client.GetAsync($"{ByScanUrl}?value={created.Barcode}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var found = await ReadAsync<ProductDto>(response);
        found.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GetByScan_WithSurroundingWhitespace_Returns200WithProduct()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var created = await CreateProductAsync(client);

        // Act
        var response = await client.GetAsync($"{ByScanUrl}?value=%20%20{created.Code}%20%20");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var found = await ReadAsync<ProductDto>(response);
        found.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GetByScan_WithUnknownValue_Returns404()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var unknown = $"UNKNOWN-{Guid.NewGuid():N}"[..20];

        // Act
        var response = await client.GetAsync($"{ByScanUrl}?value={unknown}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetByScan_WithValueBelongingOnlyToInactiveProduct_Returns404()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var created = await CreateProductAsync(client, isActive: false);

        // Act
        var response = await client.GetAsync($"{ByScanUrl}?value={created.Code}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetByScan_WithValueFromAnotherTenant_Returns404()
    {
        // Arrange — product lives in a different tenant.
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var created = await CreateProductAsync(otherTenantClient);

        // Act — scan as the seeded dev tenant.
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var response = await devClient.GetAsync($"{ByScanUrl}?value={created.Code}");

        // Assert — the other tenant's product is invisible.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<ProductDto> CreateProductAsync(HttpClient client, bool isActive = true)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var code = $"SCAN-{suffix}";
        var ean = $"590{suffix}01";
        var barcode = $"BC-{suffix}";

        var create = await client.PostAsJsonAsync(BaseUrl, new
        {
            code,
            name = $"Scan product {suffix}",
            description = (string?)null,
            ean,
            barcode,
            scanBy = 1,
            isActive,
            productGroupId = (Guid?)null,
            syncId = (string?)null
        });

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<ProductDto>(create);
    }

    private sealed record ProductDto(
        Guid Id,
        string? SyncId,
        string Code,
        string Name,
        string? Description,
        string? Ean,
        string? Barcode,
        int ScanBy,
        bool IsActive);
}
