using AsistOff.MES.IntegrationTests.Infrastructure;

namespace AsistOff.MES.IntegrationTests.Production;

/// <summary>
/// Covers <c>/api/recipes</c> CRUD plus listing/filtering. Production tests for
/// versions and operations live in their own files to keep each test focused.
/// </summary>
public sealed class RecipesEndpointsTests : IntegrationTestBase
{
    public RecipesEndpointsTests(MesApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Create_creates_recipe_with_initial_draft_version()
    {
        var created = await Client.PostJsonAsync<RecipePayload>("/api/recipes", new
        {
            Code = "REC-1",
            Name = "Recipe 1",
            Description = "Test recipe",
            IsActive = true,
            PrimaryProductId = (Guid?)null,
            SyncId = (string?)null,
        });

        created.Id.Should().NotBeEmpty();
        created.Code.Should().Be("REC-1");
        created.Versions.Should().HaveCount(1, "creating a recipe should also create version 1 (Draft)");
        created.Versions[0].VersionNumber.Should().Be(1);
        created.Versions[0].Status.Should().Be(1, "Draft = 1");
    }

    [Fact]
    public async Task Get_returns_recipe_with_versions()
    {
        var created = await Client.PostJsonAsync<RecipePayload>("/api/recipes", new
        {
            Code = "REC-2",
            Name = "Recipe 2",
            Description = (string?)null,
            IsActive = true,
            PrimaryProductId = (Guid?)null,
            SyncId = (string?)null,
        });

        var fetched = await Client.GetJsonAsync<RecipePayload>($"/api/recipes/{created.Id}");
        fetched.Id.Should().Be(created.Id);
        fetched.Versions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Browse_filters_by_code()
    {
        await Client.PostJsonAsync<RecipePayload>("/api/recipes", new
        {
            Code = "FILTER-A",
            Name = "Filter A",
            Description = (string?)null,
            IsActive = true,
            PrimaryProductId = (Guid?)null,
            SyncId = (string?)null,
        });

        await Client.PostJsonAsync<RecipePayload>("/api/recipes", new
        {
            Code = "OTHER-B",
            Name = "Other B",
            Description = (string?)null,
            IsActive = true,
            PrimaryProductId = (Guid?)null,
            SyncId = (string?)null,
        });

        var page = await Client.GetJsonAsync<PagedRecipesPayload>("/api/recipes?code=FILTER");
        page.Items.Should().ContainSingle(r => r.Code == "FILTER-A");
    }

    [Fact]
    public async Task Update_then_delete()
    {
        var created = await Client.PostJsonAsync<RecipePayload>("/api/recipes", new
        {
            Code = "REC-UPD",
            Name = "Recipe to update",
            Description = (string?)null,
            IsActive = true,
            PrimaryProductId = (Guid?)null,
            SyncId = (string?)null,
        });

        await Client.PutJsonAsync($"/api/recipes/{created.Id}", new
        {
            Id = created.Id,
            Code = "REC-UPD",
            Name = "Recipe renamed",
            Description = "now with description",
            IsActive = false,
            PrimaryProductId = (Guid?)null,
            SyncId = (string?)null,
        });

        var afterUpdate = await Client.GetJsonAsync<RecipePayload>($"/api/recipes/{created.Id}");
        afterUpdate.Name.Should().Be("Recipe renamed");
        afterUpdate.IsActive.Should().BeFalse();

        var del = await Client.DeleteAsync($"/api/recipes/{created.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var notFound = await Client.GetAsync($"/api/recipes/{created.Id}");
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_returns_conflict_for_duplicate_code()
    {
        await Client.PostJsonAsync<RecipePayload>("/api/recipes", new
        {
            Code = "DUP",
            Name = "First",
            Description = (string?)null,
            IsActive = true,
            PrimaryProductId = (Guid?)null,
            SyncId = (string?)null,
        });

        var response = await Client.PostAsJsonAsync("/api/recipes", new
        {
            Code = "DUP",
            Name = "Second",
            Description = (string?)null,
            IsActive = true,
            PrimaryProductId = (Guid?)null,
            SyncId = (string?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private sealed record VersionSummary(Guid Id, int VersionNumber, int Status);
    private sealed record RecipePayload(
        Guid Id, string Code, string Name, string? Description, bool IsActive,
        Guid? PrimaryProductId, Guid? CurrentVersionId, string? SyncId,
        IReadOnlyList<VersionSummary> Versions);
    private sealed record PagedRecipesPayload(IReadOnlyList<RecipePayload> Items, int TotalCount);
}
