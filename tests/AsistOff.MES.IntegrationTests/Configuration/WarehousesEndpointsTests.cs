using AsistOff.MES.IntegrationTests.Infrastructure;

namespace AsistOff.MES.IntegrationTests.Configuration;

public sealed class WarehousesEndpointsTests : IntegrationTestBase
{
    public WarehousesEndpointsTests(MesApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Full_crud_lifecycle()
    {
        var created = await Client.PostJsonAsync<WarehousePayload>("/api/warehouses", new
        {
            Name = "Main",
            SyncId = "wh-sync-1",
        });
        created.Id.Should().NotBeEmpty();
        created.Name.Should().Be("Main");

        var fetched = await Client.GetJsonAsync<WarehousePayload>($"/api/warehouses/{created.Id}");
        fetched.Id.Should().Be(created.Id);

        var page = await Client.GetJsonAsync<PagedWarehousesPayload>("/api/warehouses?name=Main");
        page.Items.Should().ContainSingle(w => w.Id == created.Id);

        await Client.PutJsonAsync($"/api/warehouses/{created.Id}", new
        {
            Id = created.Id,
            Name = "Main - renamed",
        });

        var afterUpdate = await Client.GetJsonAsync<WarehousePayload>($"/api/warehouses/{created.Id}");
        afterUpdate.Name.Should().Be("Main - renamed");
    }

    [Fact]
    public async Task Update_with_mismatched_route_id_returns_bad_request()
    {
        var created = await Client.PostJsonAsync<WarehousePayload>("/api/warehouses", new
        {
            Name = "Aux",
            SyncId = "wh-sync-2",
        });

        var response = await Client.PutAsJsonAsync($"/api/warehouses/{Guid.NewGuid()}", new
        {
            Id = created.Id,
            Name = "Aux",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record WarehousePayload(Guid Id, string Name, string? SyncId);
    private sealed record PagedWarehousesPayload(IReadOnlyList<WarehousePayload> Items, int TotalCount);
}
