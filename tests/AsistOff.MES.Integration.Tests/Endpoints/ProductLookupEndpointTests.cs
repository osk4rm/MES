using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for the capped <c>Search</c> typeahead on
/// <c>GET /api/products</c> (issue #274): a shopfloor lookup by code prefix
/// returns at most 20 matches ordered by code even when the caller asks for a
/// huge page.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ProductLookupEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/products";

    [Fact]
    public async Task Browse_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync($"{BaseUrl}?search=ANY");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Browse_WithSearch_ReturnsAtMost20MatchesOrderedByCode()
    {
        // Arrange - 25 products sharing one code prefix.
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = $"LK-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        for (var i = 0; i < 25; i++)
        {
            var create = await client.PostAsJsonAsync(BaseUrl, new
            {
                code = $"{tag}-{i:000}",
                name = $"Lookup product {tag} {i}",
                description = (string?)null,
                ean = UniqueEan(),
                barcode = (string?)null,
                scanBy = 2,
                isActive = true,
                productGroupId = (Guid?)null,
                syncId = (string?)null
            });
            create.EnsureSuccessStatusCode();
        }

        // Act - the typeahead asks for a huge page; the server caps it.
        var response = await client.GetAsync($"{BaseUrl}?search={tag}&pageNumber=1&pageSize=500");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<ProductDto>>(response);
        page.Items.Should().HaveCount(20);
        page.Items.Select(p => p.Code).Should().BeInAscendingOrder();
        page.Items.Should().OnlyContain(p => p.Code.Contains(tag));
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

    /// <summary>Generates a unique, check-digit-valid EAN-13 (issue #212 rejects invalid EANs with 400).</summary>
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
}
