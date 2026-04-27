using AsistOff.MES.IntegrationTests.Infrastructure;

namespace AsistOff.MES.IntegrationTests.Configuration;

public sealed class MeasureUnitsEndpointsTests : IntegrationTestBase
{
    public MeasureUnitsEndpointsTests(MesApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Full_crud_lifecycle()
    {
        var created = await Client.PostJsonAsync<MeasureUnitPayload>("/api/measure-units", new
        {
            Name = "Kilogram",
            Symbol = "kg",
            Type = 1, // Product
            ConversionFactor = (decimal?)null,
            BaseUnitId = (Guid?)null,
            IsActive = true,
            Description = "Mass unit",
            SyncId = (string?)null,
        });
        created.Id.Should().NotBeEmpty();
        created.Symbol.Should().Be("kg");

        var fetched = await Client.GetJsonAsync<MeasureUnitPayload>($"/api/measure-units/{created.Id}");
        fetched.Name.Should().Be("Kilogram");

        var page = await Client.GetJsonAsync<PagedMeasureUnitsPayload>("/api/measure-units?searchTerm=Kilo");
        page.Items.Should().Contain(m => m.Id == created.Id);

        var updateResponse = await Client.PutAsJsonAsync($"/api/measure-units/{created.Id}", new
        {
            Id = created.Id,
            Name = "Kilogram (mass)",
            Symbol = "kg",
            Type = 1,
            ConversionFactor = (decimal?)null,
            BaseUnitId = (Guid?)null,
            IsActive = true,
            Description = "Mass unit",
            SyncId = (string?)null,
        });
        updateResponse.EnsureSuccessStatusCode();

        var afterUpdate = await Client.GetJsonAsync<MeasureUnitPayload>($"/api/measure-units/{created.Id}");
        afterUpdate.Name.Should().Be("Kilogram (mass)");

        var del = await Client.DeleteAsync($"/api/measure-units/{created.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var notFound = await Client.GetAsync($"/api/measure-units/{created.Id}");
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_with_mismatched_route_id_returns_bad_request()
    {
        var created = await Client.PostJsonAsync<MeasureUnitPayload>("/api/measure-units", new
        {
            Name = "Liter",
            Symbol = "l",
            Type = 1,
            ConversionFactor = (decimal?)null,
            BaseUnitId = (Guid?)null,
            IsActive = true,
            Description = (string?)null,
            SyncId = (string?)null,
        });

        var response = await Client.PutAsJsonAsync($"/api/measure-units/{Guid.NewGuid()}", new
        {
            Id = created.Id,
            Name = "Liter",
            Symbol = "l",
            Type = 1,
            ConversionFactor = (decimal?)null,
            BaseUnitId = (Guid?)null,
            IsActive = true,
            Description = (string?)null,
            SyncId = (string?)null,
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record MeasureUnitPayload(
        Guid Id, string Name, string Symbol, int Type, decimal? ConversionFactor,
        Guid? BaseUnitId, string? BaseUnitName, bool IsActive, string? Description, string? SyncId);

    private sealed record PagedMeasureUnitsPayload(IReadOnlyList<MeasureUnitPayload> Items, int TotalCount);
}
