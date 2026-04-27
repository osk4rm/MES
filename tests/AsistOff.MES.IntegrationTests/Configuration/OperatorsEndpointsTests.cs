using AsistOff.MES.IntegrationTests.Infrastructure;

namespace AsistOff.MES.IntegrationTests.Configuration;

public sealed class OperatorsEndpointsTests : IntegrationTestBase
{
    public OperatorsEndpointsTests(MesApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Full_crud_lifecycle()
    {
        // Resolve the seeded admin's user id from the sign-in response.
        var token = await SignInAsync(MesApiFactory.AdminEmail, MesApiFactory.AdminPassword);
        var userId = Guid.Parse(token.Id);

        var department = await Client.PostJsonAsync<IdPayload>("/api/departments", new
        {
            Code = "OPS",
            Name = "Operators",
        });

        var created = await Client.PostJsonAsync<OperatorPayload>("/api/operators", new
        {
            Identifier = "OP-001",
            FirstName = "Jan",
            LastName = "Kowalski",
            RatePerHour = 75.5m,
            DepartmentId = department.Id,
            UserId = userId,
        });
        created.Id.Should().NotBeEmpty();
        created.Identifier.Should().Be("OP-001");

        var fetched = await Client.GetJsonAsync<OperatorPayload>($"/api/operators/{created.Id}");
        fetched.LastName.Should().Be("Kowalski");

        var page = await Client.GetJsonAsync<PagedOperatorsPayload>("/api/operators?identifier=OP-001");
        page.Items.Should().ContainSingle(o => o.Id == created.Id);

        await Client.PutJsonAsync($"/api/operators/{created.Id}", new
        {
            Id = created.Id,
            Identifier = "OP-001",
            FirstName = "Janek",
            LastName = "Kowalski",
            RatePerHour = 80m,
            DepartmentId = department.Id,
            UserId = userId,
        });

        var afterUpdate = await Client.GetJsonAsync<OperatorPayload>($"/api/operators/{created.Id}");
        afterUpdate.FirstName.Should().Be("Janek");

        var del = await Client.DeleteAsync($"/api/operators/{created.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var notFound = await Client.GetAsync($"/api/operators/{created.Id}");
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_with_mismatched_route_id_returns_bad_request()
    {
        var token = await SignInAsync(MesApiFactory.AdminEmail, MesApiFactory.AdminPassword);
        var userId = Guid.Parse(token.Id);

        var department = await Client.PostJsonAsync<IdPayload>("/api/departments", new
        {
            Code = "OP-DEPT",
            Name = "Operator dept",
        });

        var created = await Client.PostJsonAsync<OperatorPayload>("/api/operators", new
        {
            Identifier = "OP-X",
            FirstName = "X",
            LastName = "Y",
            RatePerHour = 10m,
            DepartmentId = department.Id,
            UserId = userId,
        });

        var response = await Client.PutAsJsonAsync($"/api/operators/{Guid.NewGuid()}", new
        {
            Id = created.Id,
            Identifier = "OP-X",
            FirstName = "X",
            LastName = "Y",
            RatePerHour = 10m,
            DepartmentId = department.Id,
            UserId = userId,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record IdPayload(Guid Id);
    private sealed record OperatorPayload(
        Guid Id, string Identifier, string FirstName, string LastName, decimal RatePerHour, string? Department);
    private sealed record PagedOperatorsPayload(IReadOnlyList<OperatorPayload> Items, int TotalCount);
}
