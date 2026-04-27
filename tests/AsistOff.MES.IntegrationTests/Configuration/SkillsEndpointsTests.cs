using AsistOff.MES.IntegrationTests.Infrastructure;

namespace AsistOff.MES.IntegrationTests.Configuration;

public sealed class SkillsEndpointsTests : IntegrationTestBase
{
    public SkillsEndpointsTests(MesApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Browse_returns_empty_paged_response_initially()
    {
        var page = await Client.GetJsonAsync<PagedSkillsPayload>("/api/skills");
        page.Should().NotBeNull();
        page.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Full_crud_lifecycle()
    {
        // Create
        var created = await Client.PostJsonAsync<SkillPayload>("/api/skills", new
        {
            Code = "WELD",
            Name = "Welding",
            Description = "Skilled welder",
            IsActive = true,
        });
        created.Id.Should().NotBeEmpty();
        created.Code.Should().Be("WELD");

        // Get
        var fetched = await Client.GetJsonAsync<SkillPayload>($"/api/skills/{created.Id}");
        fetched.Id.Should().Be(created.Id);
        fetched.Name.Should().Be("Welding");

        // Browse with filter
        var page = await Client.GetJsonAsync<PagedSkillsPayload>("/api/skills?code=WELD");
        page.Items.Should().ContainSingle(s => s.Id == created.Id);

        // Update
        await Client.PutJsonAsync($"/api/skills/{created.Id}", new
        {
            Id = created.Id,
            Code = "WELD",
            Name = "Welding (advanced)",
            Description = "Advanced welder",
            IsActive = false,
        });

        var afterUpdate = await Client.GetJsonAsync<SkillPayload>($"/api/skills/{created.Id}");
        afterUpdate.Name.Should().Be("Welding (advanced)");
        afterUpdate.IsActive.Should().BeFalse();

        // Delete
        var del = await Client.DeleteAsync($"/api/skills/{created.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var notFound = await Client.GetAsync($"/api/skills/{created.Id}");
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_with_mismatched_route_id_returns_bad_request()
    {
        var created = await Client.PostJsonAsync<SkillPayload>("/api/skills", new
        {
            Code = "MILL",
            Name = "Milling",
            Description = (string?)null,
            IsActive = true,
        });

        var response = await Client.PutAsJsonAsync($"/api/skills/{Guid.NewGuid()}", new
        {
            Id = created.Id,
            Code = "MILL",
            Name = "Milling",
            Description = (string?)null,
            IsActive = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Anonymous_request_is_unauthorized()
    {
        var response = await AnonymousClient.GetAsync("/api/skills");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record SkillPayload(Guid Id, string Code, string Name, string? Description, bool IsActive);

    private sealed record PagedSkillsPayload(IReadOnlyList<SkillPayload> Items, int TotalCount);
}
