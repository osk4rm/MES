using AsistOff.MES.IntegrationTests.Infrastructure;

namespace AsistOff.MES.IntegrationTests.Configuration;

public sealed class DepartmentsEndpointsTests : IntegrationTestBase
{
    public DepartmentsEndpointsTests(MesApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Full_crud_lifecycle()
    {
        var created = await Client.PostJsonAsync<DepartmentPayload>("/api/departments", new
        {
            Code = "PROD",
            Name = "Production",
        });
        created.Id.Should().NotBeEmpty();

        var fetched = await Client.GetJsonAsync<DepartmentPayload>($"/api/departments/{created.Id}");
        fetched.Code.Should().Be("PROD");

        var page = await Client.GetJsonAsync<PagedDepartmentsPayload>("/api/departments?code=PROD");
        page.Items.Should().ContainSingle(d => d.Id == created.Id);

        await Client.PutJsonAsync($"/api/departments/{created.Id}", new
        {
            Id = created.Id,
            Code = "PROD",
            Name = "Production - renamed",
        });

        var afterUpdate = await Client.GetJsonAsync<DepartmentPayload>($"/api/departments/{created.Id}");
        afterUpdate.Name.Should().Be("Production - renamed");

        var del = await Client.DeleteAsync($"/api/departments/{created.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var notFound = await Client.GetAsync($"/api/departments/{created.Id}");
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_with_mismatched_route_id_returns_bad_request()
    {
        var created = await Client.PostJsonAsync<DepartmentPayload>("/api/departments", new
        {
            Code = "QC",
            Name = "Quality control",
        });

        var response = await Client.PutAsJsonAsync($"/api/departments/{Guid.NewGuid()}", new
        {
            Id = created.Id,
            Code = "QC",
            Name = "Quality control",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record DepartmentPayload(Guid Id, string Code, string Name);
    private sealed record PagedDepartmentsPayload(IReadOnlyList<DepartmentPayload> Items, int TotalCount);
}
