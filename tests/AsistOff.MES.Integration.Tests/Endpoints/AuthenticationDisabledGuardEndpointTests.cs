using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Default-deny proof for issue #332: with <c>auth:AuthenticationDisabled</c>
/// unset (the test host boots with the flag off), a permission-guarded write
/// still requires a credential — an unauthenticated request yields 401 while
/// the JWT path keeps working. Runs against the real HTTP pipeline.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class AuthenticationDisabledGuardEndpointTests(MesApplicationFixture fixture)
    : IntegrationTestBase(fixture)
{
    private const string ProductsUrl = "/api/products";

    [Fact]
    public async Task CreateProduct_WithoutToken_Returns401()
    {
        // Arrange — no credential of any kind.
        using var client = Fixture.CreateClient();
        var code = $"ADG-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        // Act
        var response = await client.PostAsJsonAsync(ProductsUrl, CreatePayload(code));

        // Assert — default-deny holds with the bypass flag off.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateProduct_WithToken_Succeeds()
    {
        // Arrange — the JWT + RBAC path enforces and then admits.
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = $"ADG-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        // Act
        var response = await client.PostAsJsonAsync(ProductsUrl, CreatePayload(code));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private static object CreatePayload(string code) => new
    {
        code,
        name = $"Auth guard product {code}",
        description = (string?)null,
        ean = (string?)null,
        barcode = (string?)null,
        scanBy = 1,
        isActive = true,
        productGroupId = (Guid?)null,
        syncId = (string?)null
    };
}
