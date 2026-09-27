using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint proof for issue #311 against the representative
/// <c>GET /api/machines</c> browse (whitelist <c>Name, Code, IsActive</c>):
/// whitelisted sorts stay 200 and ordered, injection or unknown sorts are
/// 400, and unauthenticated sorts stay 401.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class SortWhitelistEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/machines";

    [Theory]
    [InlineData("Code", "asc")]
    [InlineData("Code", "desc")]
    [InlineData("Name", "asc")]
    [InlineData("Name", "desc")]
    [InlineData("IsActive", "asc")]
    [InlineData("IsActive", "desc")]
    public async Task Browse_WhitelistedSort_Returns200(string field, string order)
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync($"{BaseUrl}?RawSort={field},{order}&PageSize=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<MachineDto>>(response);
        page.Items.Should().NotBeNull();
    }

    [Fact]
    public async Task Browse_WhitelistedSort_OrdersByCode()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var prefix = $"SWL-{Guid.NewGuid():N}"[..12];
        var codeA = $"{prefix}-A";
        var codeB = $"{prefix}-B";
        await CreateMachineAsync(client, codeA, "Sort Whitelist A");
        await CreateMachineAsync(client, codeB, "Sort Whitelist B");

        // Act
        var asc = await client.GetAsync($"{BaseUrl}?code={prefix}&RawSort=Code,asc&PageSize=10");
        var desc = await client.GetAsync($"{BaseUrl}?code={prefix}&RawSort=Code,desc&PageSize=10");

        // Assert
        asc.StatusCode.Should().Be(HttpStatusCode.OK);
        desc.StatusCode.Should().Be(HttpStatusCode.OK);
        var ascPage = await ReadAsync<PagedResponseDto<MachineDto>>(asc);
        var descPage = await ReadAsync<PagedResponseDto<MachineDto>>(desc);
        ascPage.Items.Select(m => m.Code).Should().Equal(codeA, codeB);
        descPage.Items.Select(m => m.Code).Should().Equal(codeB, codeA);
    }

    [Theory]
    // Injection payload from the issue: space-separated field plus an extra
    // non-whitelisted property in a single RawSort entry.
    [InlineData("Code desc, TenantId")]
    // Dynamic LINQ method call probe.
    [InlineData("Code.Equals(\"x\")")]
    // Navigation traversal probe.
    [InlineData("Department.Code")]
    // Expression probe.
    [InlineData("1==1")]
    public async Task Browse_InjectionSort_Returns400(string rawSort)
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync($"{BaseUrl}?RawSort={Uri.EscapeDataString(rawSort)}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Browse_UnsupportedField_Returns400()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync($"{BaseUrl}?RawSort=NoSuchField,asc");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("NoSuchField");
    }

    [Fact]
    public async Task Browse_WithoutToken_Returns401RegardlessOfSort()
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.GetAsync($"{BaseUrl}?RawSort=Code,asc");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Browse_InjectionSortWithoutToken_Returns401()
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.GetAsync(
            $"{BaseUrl}?RawSort={Uri.EscapeDataString("Code desc, TenantId")}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task CreateMachineAsync(HttpClient client, string code, string name)
    {
        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            code,
            name,
            description = (string?)null,
            isActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
