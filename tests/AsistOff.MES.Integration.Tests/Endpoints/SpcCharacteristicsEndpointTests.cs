using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>/api/spc-characteristics</c>. They
/// exercise the full request pipeline: authentication, tenant resolution,
/// MediatR handler, EF Core persistence and the global exception handler -
/// against a real PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class SpcCharacteristicsEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/spc-characteristics";

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
        var payload = ValidPayload(code);

        var createResponse = await client.PostAsJsonAsync(BaseUrl, payload);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<SpcCharacteristicDto>(createResponse);
        created.Code.Should().Be(code);
        created.Id.Should().NotBeEmpty();
        created.ChartType.Should().Be(1);
        created.LowerSpecLimit.Should().Be(9.5m);
        created.UpperSpecLimit.Should().Be(10.5m);

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadAsync<SpcCharacteristicDto>(getResponse);
        fetched.Id.Should().Be(created.Id);
        fetched.Name.Should().Be("Shaft diameter");
        fetched.SampleSize.Should().Be(5);
    }

    [Fact]
    public async Task Create_WithDuplicateCode_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();

        var first = await client.PostAsJsonAsync(BaseUrl, ValidPayload(code));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync(BaseUrl, ValidPayload(code));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_WithInvertedSpecLimits_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var payload = ValidPayload(UniqueCode()) with { LowerSpecLimit = 10.5m, UpperSpecLimit = 9.5m };

        var response = await client.PostAsJsonAsync(BaseUrl, payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithInvertedControlLimits_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var payload = ValidPayload(UniqueCode()) with { LowerControlLimit = 10.2m, UpperControlLimit = 9.8m };

        var response = await client.PostAsJsonAsync(BaseUrl, payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithNominalOutsideSpecLimits_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var payload = ValidPayload(UniqueCode()) with { NominalValue = 11m };

        var response = await client.PostAsJsonAsync(BaseUrl, payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithSampleSizeBelowOne_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var payload = ValidPayload(UniqueCode()) with { SampleSize = 0 };

        var response = await client.PostAsJsonAsync(BaseUrl, payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_WithUnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Browse_WithFilters_ReturnsMatchingItem()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();
        var productId = Guid.NewGuid();
        var create = await client.PostAsJsonAsync(BaseUrl, ValidPayload(code) with { ProductId = productId });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<SpcCharacteristicDto>(create);

        var byProduct = await client.GetAsync($"{BaseUrl}?productId={productId}");
        byProduct.StatusCode.Should().Be(HttpStatusCode.OK);
        var productPage = await ReadAsync<PagedResponseDto<SpcCharacteristicDto>>(byProduct);
        productPage.Items.Should().ContainSingle(item => item.Id == created.Id);

        var bySearch = await client.GetAsync($"{BaseUrl}?search={code}");
        bySearch.StatusCode.Should().Be(HttpStatusCode.OK);
        var searchPage = await ReadAsync<PagedResponseDto<SpcCharacteristicDto>>(bySearch);
        searchPage.Items.Should().ContainSingle(item => item.Code == code);

        var inactive = await client.GetAsync($"{BaseUrl}?isActive=false");
        inactive.StatusCode.Should().Be(HttpStatusCode.OK);
        var inactivePage = await ReadAsync<PagedResponseDto<SpcCharacteristicDto>>(inactive);
        inactivePage.Items.Should().NotContain(item => item.Id == created.Id);
    }

    [Fact]
    public async Task Update_ReturnsNoContent_AndPersistsChange()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var code = UniqueCode();
        var create = await client.PostAsJsonAsync(BaseUrl, ValidPayload(code));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<SpcCharacteristicDto>(create);

        var update = await client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", new
        {
            id = created.Id,
            name = "Shaft diameter (updated)",
            description = "Updated definition",
            productId = (Guid?)null,
            machineId = (Guid?)null,
            chartType = 2,
            nominalValue = 10m,
            lowerSpecLimit = 9.5m,
            upperSpecLimit = 10.5m,
            lowerControlLimit = 9.8m,
            upperControlLimit = 10.2m,
            sampleSize = 4,
            unit = "mm",
            isActive = false
        });

        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await client.GetAsync($"{BaseUrl}/{created.Id}");
        var fetched = await ReadAsync<SpcCharacteristicDto>(get);
        fetched.Name.Should().Be("Shaft diameter (updated)");
        fetched.ChartType.Should().Be(2);
        fetched.SampleSize.Should().Be(4);
        fetched.IsActive.Should().BeFalse();
        fetched.Code.Should().Be(code);
    }

    [Fact]
    public async Task Update_WithUnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var id = Guid.NewGuid();

        var response = await client.PutAsJsonAsync($"{BaseUrl}/{id}", new
        {
            id,
            name = "Missing",
            description = (string?)null,
            productId = (Guid?)null,
            machineId = (Guid?)null,
            chartType = 1,
            nominalValue = (decimal?)null,
            lowerSpecLimit = (decimal?)null,
            upperSpecLimit = (decimal?)null,
            lowerControlLimit = (decimal?)null,
            upperControlLimit = (decimal?)null,
            sampleSize = 5,
            unit = (string?)null,
            isActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_RemovesCharacteristic_AndUnknownIdReturns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var create = await client.PostAsJsonAsync(BaseUrl, ValidPayload(UniqueCode()));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<SpcCharacteristicDto>(create);

        var delete = await client.DeleteAsync($"{BaseUrl}/{created.Id}");

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await client.GetAsync($"{BaseUrl}/{created.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var missing = await client.DeleteAsync($"{BaseUrl}/{Guid.NewGuid()}");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_OtherTenantRecord_Returns404()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);

        var create = await otherTenantClient.PostAsJsonAsync(BaseUrl, ValidPayload(UniqueCode()));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<SpcCharacteristicDto>(create);

        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var response = await devClient.GetAsync($"{BaseUrl}/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static string UniqueCode() => $"SPC-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    private sealed record SpcPayload(
        string Code,
        string Name,
        string? Description,
        Guid? ProductId,
        Guid? MachineId,
        int ChartType,
        decimal? NominalValue,
        decimal? LowerSpecLimit,
        decimal? UpperSpecLimit,
        decimal? LowerControlLimit,
        decimal? UpperControlLimit,
        int SampleSize,
        string? Unit,
        bool IsActive);

    private static SpcPayload ValidPayload(string code) => new(
        code, "Shaft diameter", null, null, null, 1,
        10m, 9.5m, 10.5m, 9.8m, 10.2m, 5, "mm", true);
}
