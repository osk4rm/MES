using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>/api/spc-measurements</c>. They
/// exercise the full request pipeline: authentication, tenant resolution,
/// MediatR handler, EF Core persistence and the global exception handler -
/// against a real PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class SpcMeasurementsEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/spc-measurements";
    private const string CharacteristicsUrl = "/api/spc-characteristics";

    [Fact]
    public async Task Browse_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Record_ReturnsCreated_AndIsRetrievableById()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var characteristicId = await CreateCharacteristicAsync(client, UniqueCode());

        var measuredAt = DateTime.UtcNow.AddMinutes(-10);
        var createResponse = await client.PostAsJsonAsync(BaseUrl, new
        {
            characteristicId,
            value = 10.01m,
            measuredAt,
            notes = "first article"
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<SpcMeasurementDto>(createResponse);
        created.CharacteristicId.Should().Be(characteristicId);
        created.Value.Should().Be(10.01m);
        created.Id.Should().NotBeEmpty();

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadAsync<SpcMeasurementDto>(getResponse);
        fetched.Id.Should().Be(created.Id);
        fetched.Value.Should().Be(10.01m);
        fetched.Notes.Should().Be("first article");
    }

    [Fact]
    public async Task Record_UnknownCharacteristic_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            characteristicId = Guid.NewGuid(),
            value = 10m,
            measuredAt = DateTime.UtcNow.AddMinutes(-5),
            notes = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Record_InactiveCharacteristic_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var characteristicId = await CreateCharacteristicAsync(client, UniqueCode(), isActive: false);

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            characteristicId,
            value = 10m,
            measuredAt = DateTime.UtcNow.AddMinutes(-5),
            notes = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Record_FutureMeasuredAt_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var characteristicId = await CreateCharacteristicAsync(client, UniqueCode());

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            characteristicId,
            value = 10m,
            measuredAt = DateTime.UtcNow.AddHours(1),
            notes = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Browse_FiltersByCharacteristic_ReturnsAscendingOrderWithPaging()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var characteristicId = await CreateCharacteristicAsync(client, UniqueCode());
        var otherCharacteristicId = await CreateCharacteristicAsync(client, UniqueCode());

        var baseTime = DateTime.UtcNow.AddHours(-1);
        // Record out of chronological order; the browse must return ascending.
        await RecordAsync(client, characteristicId, 10.2m, baseTime.AddMinutes(30));
        await RecordAsync(client, characteristicId, 10.0m, baseTime.AddMinutes(10));
        await RecordAsync(client, characteristicId, 10.1m, baseTime.AddMinutes(20));
        await RecordAsync(client, otherCharacteristicId, 99.9m, baseTime.AddMinutes(15));

        var page1 = await client.GetAsync(
            $"{BaseUrl}?characteristicId={characteristicId}&pageNumber=1&pageSize=2");
        page1.StatusCode.Should().Be(HttpStatusCode.OK);
        var page1Body = await ReadAsync<PagedResponseDto<SpcMeasurementDto>>(page1);
        page1Body.TotalCount.Should().Be(3);
        page1Body.Items.Should().HaveCount(2);
        page1Body.Items.Select(i => i.Value).Should().ContainInOrder(10.0m, 10.1m);

        var page2 = await client.GetAsync(
            $"{BaseUrl}?characteristicId={characteristicId}&pageNumber=2&pageSize=2");
        var page2Body = await ReadAsync<PagedResponseDto<SpcMeasurementDto>>(page2);
        page2Body.Items.Should().ContainSingle(i => i.Value == 10.2m);

        var filtered = await client.GetAsync($"{BaseUrl}?characteristicId={otherCharacteristicId}");
        var filteredBody = await ReadAsync<PagedResponseDto<SpcMeasurementDto>>(filtered);
        filteredBody.Items.Should().ContainSingle(i => i.Value == 99.9m);
    }

    [Fact]
    public async Task Chart_ReturnsLimitsAndFlags()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var characteristicId = await CreateCharacteristicAsync(client, UniqueCode());
        var baseTime = DateTime.UtcNow.AddHours(-1);
        await RecordAsync(client, characteristicId, 10.0m, baseTime.AddMinutes(10));
        await RecordAsync(client, characteristicId, 10.3m, baseTime.AddMinutes(20));
        await RecordAsync(client, characteristicId, 10.9m, baseTime.AddMinutes(30));

        var response = await client.GetAsync($"{BaseUrl}/chart?characteristicId={characteristicId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var chart = await ReadAsync<SpcMeasurementChartDto>(response);
        chart.CharacteristicId.Should().Be(characteristicId);
        chart.NominalValue.Should().Be(10m);
        chart.LowerSpecLimit.Should().Be(9.5m);
        chart.UpperSpecLimit.Should().Be(10.5m);
        chart.LowerControlLimit.Should().Be(9.8m);
        chart.UpperControlLimit.Should().Be(10.2m);
        chart.TotalCount.Should().Be(3);
        chart.OutOfControlCount.Should().Be(2);
        chart.OutOfSpecCount.Should().Be(1);
        var ordered = chart.Points.OrderBy(p => p.MeasuredAt).ToList();
        ordered[0].IsOutOfControl.Should().BeFalse();
        ordered[0].IsOutOfSpec.Should().BeFalse();
        ordered[1].IsOutOfControl.Should().BeTrue();
        ordered[1].IsOutOfSpec.Should().BeFalse();
        ordered[2].IsOutOfControl.Should().BeTrue();
        ordered[2].IsOutOfSpec.Should().BeTrue();
    }

    [Fact]
    public async Task Chart_WithoutControlLimits_NeverMarksOutOfControl()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var characteristicId = await CreateCharacteristicAsync(
            client, UniqueCode(), lowerControlLimit: null, upperControlLimit: null);
        await RecordAsync(client, characteristicId, 99.99m, DateTime.UtcNow.AddMinutes(-10));

        var response = await client.GetAsync($"{BaseUrl}/chart?characteristicId={characteristicId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var chart = await ReadAsync<SpcMeasurementChartDto>(response);
        var point = chart.Points.Should().ContainSingle().Subject;
        point.IsOutOfControl.Should().BeFalse();
        point.IsOutOfSpec.Should().BeTrue();
        chart.OutOfControlCount.Should().Be(0);
    }

    [Fact]
    public async Task Chart_UnknownCharacteristic_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{BaseUrl}/chart?characteristicId={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_OtherTenantMeasurement_Returns404_AndBrowseExcludes()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var otherCharacteristicId = await CreateCharacteristicAsync(otherTenantClient, UniqueCode());
        var otherMeasurementId = await RecordAsync(
            otherTenantClient, otherCharacteristicId, 10.0m, DateTime.UtcNow.AddMinutes(-10));

        using var devClient = await Fixture.CreateAuthenticatedClientAsync();

        var get = await devClient.GetAsync($"{BaseUrl}/{otherMeasurementId}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var browse = await devClient.GetAsync($"{BaseUrl}?characteristicId={otherCharacteristicId}");
        browse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<SpcMeasurementDto>>(browse);
        page.Items.Should().BeEmpty();

        var chart = await devClient.GetAsync($"{BaseUrl}/chart?characteristicId={otherCharacteristicId}");
        chart.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static string UniqueCode() => $"SPC-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    private static async Task<Guid> CreateCharacteristicAsync(
        HttpClient client,
        string code,
        bool isActive = true,
        decimal? lowerControlLimit = 9.8m,
        decimal? upperControlLimit = 10.2m)
    {
        var create = await client.PostAsJsonAsync(CharacteristicsUrl, new
        {
            code,
            name = "Shaft diameter",
            description = (string?)null,
            productId = (Guid?)null,
            machineId = (Guid?)null,
            chartType = 1,
            nominalValue = (decimal?)10m,
            lowerSpecLimit = (decimal?)9.5m,
            upperSpecLimit = (decimal?)10.5m,
            lowerControlLimit,
            upperControlLimit,
            sampleSize = 5,
            unit = "mm",
            isActive
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<SpcCharacteristicDto>(create);
        return created.Id;
    }

    private static async Task<Guid> RecordAsync(HttpClient client, Guid characteristicId, decimal value, DateTime measuredAt)
    {
        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            characteristicId,
            value,
            measuredAt,
            notes = (string?)null
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<SpcMeasurementDto>(response);
        return created.Id;
    }
}
