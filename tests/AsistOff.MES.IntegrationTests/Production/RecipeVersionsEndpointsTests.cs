using AsistOff.MES.IntegrationTests.Infrastructure;

namespace AsistOff.MES.IntegrationTests.Production;

/// <summary>
/// Covers <c>/api/recipe-versions</c>: create, clone, release, update metadata, delete.
/// </summary>
public sealed class RecipeVersionsEndpointsTests : IntegrationTestBase
{
    public RecipeVersionsEndpointsTests(MesApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Create_clone_release_lifecycle()
    {
        var recipeId = await CreateRecipeAsync();

        // Recipe creation already added version 1 (Draft); add a second draft.
        var draft = await Client.PostJsonAsync<VersionDetailPayload>("/api/recipe-versions", new
        {
            RecipeId = recipeId,
            ChangeNotes = "second draft",
            ValidFrom = (DateTime?)null,
            ValidTo = (DateTime?)null,
        });
        draft.RecipeId.Should().Be(recipeId);
        draft.VersionNumber.Should().Be(2);
        draft.Status.Should().Be(1);

        // Update metadata
        await Client.PutJsonAsync($"/api/recipe-versions/{draft.Id}/metadata", new
        {
            VersionId = draft.Id,
            ChangeNotes = "second draft (edited)",
            ValidFrom = DateTime.UtcNow.Date,
            ValidTo = DateTime.UtcNow.Date.AddDays(30),
        });

        var afterMetadataUpdate = await Client.GetJsonAsync<VersionDetailPayload>($"/api/recipe-versions/{draft.Id}");
        afterMetadataUpdate.ChangeNotes.Should().Be("second draft (edited)");

        // A version must contain at least one operation to be released.
        await AddOperationAsync(draft.Id, "OP-RELEASE");

        // Release
        var releaseResponse = await Client.PostAsync($"/api/recipe-versions/{draft.Id}/release", content: null);
        releaseResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterRelease = await Client.GetJsonAsync<VersionDetailPayload>($"/api/recipe-versions/{draft.Id}");
        afterRelease.Status.Should().Be(2, "Released = 2");

        // Clone the released version into a new draft
        var clone = await Client.PostJsonAsync<VersionDetailPayload>("/api/recipe-versions/clone", new
        {
            SourceVersionId = draft.Id,
            ChangeNotes = "cloned",
            ValidFrom = (DateTime?)null,
            ValidTo = (DateTime?)null,
        });
        clone.Status.Should().Be(1, "clone results in a new Draft version");
        clone.Id.Should().NotBe(draft.Id);

        // Delete the cloned draft
        var deleteResponse = await Client.DeleteAsync($"/api/recipe-versions/{clone.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var notFound = await Client.GetAsync($"/api/recipe-versions/{clone.Id}");
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Cannot_delete_released_version()
    {
        var recipeId = await CreateRecipeAsync();
        var draft = await Client.PostJsonAsync<VersionDetailPayload>("/api/recipe-versions", new
        {
            RecipeId = recipeId,
            ChangeNotes = (string?)null,
            ValidFrom = (DateTime?)null,
            ValidTo = (DateTime?)null,
        });
        await AddOperationAsync(draft.Id, "OP-DEL");

        var releaseResponse = await Client.PostAsync($"/api/recipe-versions/{draft.Id}/release", content: null);
        releaseResponse.EnsureSuccessStatusCode();

        var deleteResponse = await Client.DeleteAsync($"/api/recipe-versions/{draft.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "released versions are immutable and cannot be deleted");
    }

    [Fact]
    public async Task UpdateMetadata_with_mismatched_route_id_returns_bad_request()
    {
        var recipeId = await CreateRecipeAsync();
        var draft = await Client.PostJsonAsync<VersionDetailPayload>("/api/recipe-versions", new
        {
            RecipeId = recipeId,
            ChangeNotes = (string?)null,
            ValidFrom = (DateTime?)null,
            ValidTo = (DateTime?)null,
        });

        var response = await Client.PutAsJsonAsync($"/api/recipe-versions/{Guid.NewGuid()}/metadata", new
        {
            VersionId = draft.Id,
            ChangeNotes = "x",
            ValidFrom = (DateTime?)null,
            ValidTo = (DateTime?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<Guid> CreateRecipeAsync()
    {
        var recipe = await Client.PostJsonAsync<RecipePayload>("/api/recipes", new
        {
            Code = "RV-" + Guid.NewGuid().ToString("N")[..8],
            Name = "Recipe",
            Description = (string?)null,
            IsActive = true,
            PrimaryProductId = (Guid?)null,
            SyncId = (string?)null,
        });
        return recipe.Id;
    }

    private async Task AddOperationAsync(Guid versionId, string code)
    {
        var response = await Client.PostAsJsonAsync("/api/operations", new
        {
            VersionId = versionId,
            Code = code,
            Name = code,
            Description = (string?)null,
            OperationType = (string?)null,
            SortIndex = (int?)null,
            SetupTimeMinutes = (decimal?)null,
            RunTimeMode = 1,
            RunTimePerUnitSeconds = (decimal?)null,
            RunTimePerBatchMinutes = (decimal?)null,
            TeardownTimeMinutes = (decimal?)null,
            QueueTimeMinutes = (decimal?)null,
            IsOptional = false,
            AllowParallelExecution = false,
            ExpectedQuantity = (decimal?)null,
        });
        response.EnsureSuccessStatusCode();
    }

    private sealed record RecipePayload(Guid Id);

    private sealed record VersionDetailPayload(
        Guid Id, Guid RecipeId, int VersionNumber, int Status,
        DateTime? ReleasedAt, DateTime? ValidFrom, DateTime? ValidTo, string? ChangeNotes);
}
