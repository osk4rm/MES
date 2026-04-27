using AsistOff.MES.IntegrationTests.Infrastructure;

namespace AsistOff.MES.IntegrationTests.Configuration;

public sealed class MachinesEndpointsTests : IntegrationTestBase
{
    public MachinesEndpointsTests(MesApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Full_crud_lifecycle_including_department_link()
    {
        // Need a department to link the machine to.
        var department = await Client.PostJsonAsync<IdPayload>("/api/departments", new
        {
            Code = "M-DEPT",
            Name = "Machines department",
        });

        var created = await Client.PostJsonAsync<MachinePayload>("/api/machines", new
        {
            Code = "MACH-1",
            Name = "Machine 1",
            Description = "Test machine",
            IsActive = true,
            DepartmentId = department.Id,
            SyncId = (string?)null,
        });
        created.Id.Should().NotBeEmpty();
        created.DepartmentId.Should().Be(department.Id);

        var fetched = await Client.GetJsonAsync<MachinePayload>($"/api/machines/{created.Id}");
        fetched.Code.Should().Be("MACH-1");

        var page = await Client.GetJsonAsync<PagedMachinesPayload>($"/api/machines?departmentId={department.Id}");
        page.Items.Should().Contain(m => m.Id == created.Id);

        await Client.PutJsonAsync($"/api/machines/{created.Id}", new
        {
            Id = created.Id,
            Code = "MACH-1",
            Name = "Machine 1 - renamed",
            Description = "Test machine",
            IsActive = false,
            DepartmentId = department.Id,
            SyncId = (string?)null,
        });

        var afterUpdate = await Client.GetJsonAsync<MachinePayload>($"/api/machines/{created.Id}");
        afterUpdate.Name.Should().Be("Machine 1 - renamed");
        afterUpdate.IsActive.Should().BeFalse();

        var del = await Client.DeleteAsync($"/api/machines/{created.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var notFound = await Client.GetAsync($"/api/machines/{created.Id}");
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_with_mismatched_route_id_returns_bad_request()
    {
        var created = await Client.PostJsonAsync<MachinePayload>("/api/machines", new
        {
            Code = "MACH-X",
            Name = "Machine X",
            Description = (string?)null,
            IsActive = true,
            DepartmentId = (Guid?)null,
            SyncId = (string?)null,
        });

        var response = await Client.PutAsJsonAsync($"/api/machines/{Guid.NewGuid()}", new
        {
            Id = created.Id,
            Code = "MACH-X",
            Name = "Machine X",
            Description = (string?)null,
            IsActive = true,
            DepartmentId = (Guid?)null,
            SyncId = (string?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record IdPayload(Guid Id);

    private sealed record MachinePayload(
        Guid Id, string Code, string Name, string? Description, bool IsActive,
        Guid? DepartmentId, string? DepartmentCode, string? DepartmentName, string? SyncId);

    private sealed record PagedMachinesPayload(IReadOnlyList<MachinePayload> Items, int TotalCount);
}
