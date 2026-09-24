using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint tests for <c>POST /api/recipe-versions/{id}/release</c> covering
/// issue #88 finding 3: releasing an empty recipe version must fail with
/// 400 and a descriptive message (which the UI surfaces via toast + inline error).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class RecipeReleaseEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Release_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.PostAsync($"/api/recipe-versions/{Guid.NewGuid()}/release", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Release_EmptyVersion_Returns400_WithMessage()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var recipeId = await CreateRecipeAsync(client);
        var versionId = await CreateVersionAsync(client, recipeId);

        // Act
        var release = await client.PostAsync($"/api/recipe-versions/{versionId}/release", null);

        // Assert
        release.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await release.Content.ReadAsStringAsync();
        body.Should().Contain("at least one operation");
    }

    [Fact]
    public async Task Release_UnknownVersion_Returns404()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        // Act
        var release = await client.PostAsync($"/api/recipe-versions/{Guid.NewGuid()}/release", null);

        // Assert
        release.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Release_VersionWithOperation_Returns204()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var recipeId = await CreateRecipeAsync(client);
        var versionId = await CreateVersionAsync(client, recipeId);
        await AddOperationAsync(client, versionId);

        // Act
        var release = await client.PostAsync($"/api/recipe-versions/{versionId}/release", null);

        // Assert
        release.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static async Task<Guid> CreateRecipeAsync(HttpClient client)
    {
        var code = $"R-{Guid.NewGuid():N}"[..10];
        var response = await client.PostAsJsonAsync("/api/recipes", new
        {
            code,
            name = $"Recipe {code}",
            description = (string?)null,
            isActive = true,
            primaryProductId = (Guid?)null,
            syncId = (string?)null
        });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<RecipeDto>();
        return created!.Id;
    }

    private static async Task<Guid> CreateVersionAsync(HttpClient client, Guid recipeId)
    {
        var response = await client.PostAsJsonAsync("/api/recipe-versions", new
        {
            recipeId,
            changeNotes = (string?)null,
            validFrom = (DateTime?)null,
            validTo = (DateTime?)null
        });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<VersionDto>();
        return created!.Id;
    }

    private static async Task AddOperationAsync(HttpClient client, Guid versionId)
    {
        var response = await client.PostAsJsonAsync("/api/operations", new
        {
            versionId,
            code = $"OP-{Guid.NewGuid():N}"[..10],
            name = "Cutting",
            description = (string?)null,
            operationType = (string?)null,
            sortIndex = (int?)null,
            setupTimeMinutes = (decimal?)null,
            runTimeMode = 1,
            runTimePerUnitSeconds = (decimal?)30,
            runTimePerBatchMinutes = (decimal?)null,
            teardownTimeMinutes = (decimal?)null,
            queueTimeMinutes = (decimal?)null,
            isOptional = false,
            allowParallelExecution = false,
            expectedQuantity = (decimal?)null
        });
        response.EnsureSuccessStatusCode();
    }

    private sealed record RecipeDto(Guid Id, string Code, string Name);
    private sealed record VersionDto(Guid Id, Guid RecipeId, int VersionNumber, int Status);
}
