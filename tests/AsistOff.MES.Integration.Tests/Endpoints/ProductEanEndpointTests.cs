using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint tests for the EAN/GTIN hardening from issue #212:
/// check-digit validation (400), tenant-scoped EAN uniqueness (409),
/// cross-tenant reuse, and EAN resolution through <c>GET /api/products/by-scan</c>.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ProductEanEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/products";
    private const string ByScanUrl = "/api/products/by-scan";

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(BaseUrl, CreatePayload(UniqueCode(), "5901234123457"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_WithBadCheckDigitEan_Returns400AndStoresNothing()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();
        const string badEan = "5901234123458";

        // Act
        var response = await client.PostAsJsonAsync(BaseUrl, CreatePayload(code, badEan));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var byScan = await client.GetAsync($"{ByScanUrl}?value={badEan}");
        byScan.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_WithNonDigitEan_Returns400()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.PostAsJsonAsync(BaseUrl, CreatePayload(UniqueCode(), "59012341234A"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithDuplicateEanInSameTenant_Returns409()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var ean = UniqueEan();
        var first = await client.PostAsJsonAsync(BaseUrl, CreatePayload(UniqueCode(), ean));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act
        var second = await client.PostAsJsonAsync(BaseUrl, CreatePayload(UniqueCode(), ean));

        // Assert
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_WithSameEanInAnotherTenant_Succeeds()
    {
        // Arrange — same EAN already lives in a different tenant.
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var ean = UniqueEan();
        var other = await otherTenantClient.PostAsJsonAsync(BaseUrl, CreatePayload(UniqueCode(), ean));
        other.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act — reuse the EAN in the seeded dev tenant.
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var response = await devClient.PostAsJsonAsync(BaseUrl, CreatePayload(UniqueCode(), ean));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ProductDto>(response);
        created.Ean.Should().Be(ean);
    }

    [Fact]
    public async Task Update_WithBadCheckDigitEan_Returns400AndKeepsOldEan()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var ean = UniqueEan();
        var created = await CreateProductAsync(client, ean);

        // Act
        var update = await client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", UpdatePayload(created, "5901234123458"));

        // Assert
        update.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var get = await client.GetAsync($"{BaseUrl}/{created.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadAsync<ProductDto>(get);
        fetched.Ean.Should().Be(ean);
    }

    [Fact]
    public async Task Update_WithDuplicateEan_Returns409()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var first = await CreateProductAsync(client, UniqueEan());
        var second = await CreateProductAsync(client, UniqueEan());

        // Act — move the second product onto the first product's EAN.
        var update = await client.PutAsJsonAsync($"{BaseUrl}/{second.Id}", UpdatePayload(second, first.Ean!));

        // Assert
        update.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_WithNullOrEmptyEan_Succeeds()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        // Act
        var withNull = await client.PostAsJsonAsync(BaseUrl, CreatePayload(UniqueCode(), null));
        var withEmpty = await client.PostAsJsonAsync(BaseUrl, CreatePayload(UniqueCode(), "   "));

        // Assert — products without an EAN keep working unchanged.
        withNull.StatusCode.Should().Be(HttpStatusCode.Created);
        withEmpty.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReadAsync<ProductDto>(withNull)).Ean.Should().BeNull();
        (await ReadAsync<ProductDto>(withEmpty)).Ean.Should().BeNull();
    }

    [Fact]
    public async Task GetByScan_WithValidEan_ReturnsCallerTenantProduct()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var created = await CreateProductAsync(client, UniqueEan());

        // Act
        var response = await client.GetAsync($"{ByScanUrl}?value={created.Ean}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var found = await ReadAsync<ProductDto>(response);
        found.Id.Should().Be(created.Id);
        found.Ean.Should().Be(created.Ean);
    }

    [Fact]
    public async Task GetByScan_WithCrossTenantEan_Returns404()
    {
        // Arrange — the EAN lives only in another tenant.
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var created = await CreateProductAsync(otherTenantClient, UniqueEan());

        // Act — scan as the seeded dev tenant.
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var response = await devClient.GetAsync($"{ByScanUrl}?value={created.Ean}");

        // Assert — no data leak across tenants.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetByScan_WithEanOfInactiveProduct_Returns404()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var created = await CreateProductAsync(client, UniqueEan(), isActive: false);

        // Act
        var response = await client.GetAsync($"{ByScanUrl}?value={created.Ean}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static object CreatePayload(string code, string? ean) => new
    {
        code,
        name = $"EAN product {code}",
        description = (string?)null,
        ean,
        barcode = (string?)null,
        scanBy = 1,
        isActive = true,
        productGroupId = (Guid?)null,
        syncId = (string?)null
    };

    private static object UpdatePayload(ProductDto current, string? ean) => new
    {
        id = current.Id,
        code = current.Code,
        name = current.Name,
        description = (string?)null,
        ean,
        barcode = (string?)null,
        scanBy = 1,
        isActive = current.IsActive,
        productGroupId = (Guid?)null,
        syncId = (string?)null
    };

    private static async Task<ProductDto> CreateProductAsync(HttpClient client, string ean, bool isActive = true)
    {
        var code = UniqueCode();
        var create = await client.PostAsJsonAsync(BaseUrl, new
        {
            code,
            name = $"EAN product {code}",
            description = (string?)null,
            ean,
            barcode = (string?)null,
            scanBy = 1,
            isActive,
            productGroupId = (Guid?)null,
            syncId = (string?)null
        });

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<ProductDto>(create);
    }

    private static string UniqueCode() => $"EAN-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    /// <summary>Generates a unique, check-digit-valid EAN-13.</summary>
    private static string UniqueEan()
    {
        var prefix = new char[12];
        prefix[0] = '5';
        prefix[1] = '9';
        prefix[2] = '0';
        for (var i = 3; i < 12; i++)
            prefix[i] = (char)('0' + Random.Shared.Next(10));

        var body = new string(prefix);
        for (var check = 0; check <= 9; check++)
        {
            var candidate = body + check;
            if (HasValidCheckDigit(candidate))
                return candidate;
        }

        throw new InvalidOperationException("Unable to compute an EAN check digit.");
    }

    private static bool HasValidCheckDigit(string value)
    {
        var sum = 0;
        for (var i = 0; i < value.Length; i++)
            sum += (value[i] - '0') * ((value.Length - i) % 2 == 0 ? 3 : 1);

        return sum % 10 == 0;
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
