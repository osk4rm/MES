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

    [Fact]
    public async Task Release_VersionWithOperationAndOutput_Returns204()
    {
        // Arrange: warn-state rules (no warehouses set) do not block the release.
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var product = await CreateProductAsync(client, isActive: true);
        var recipeId = await CreateRecipeAsync(client);
        var versionId = await CreateVersionAsync(client, recipeId);
        var operationId = await AddOperationAsync(client, versionId);
        await AddOutputAsync(client, operationId, product.Id);

        // Act
        var release = await client.PostAsync($"/api/recipe-versions/{versionId}/release", null);

        // Assert
        release.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Release_DraftWithIncoherentValidity_Returns400_AndStaysDraft()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var product = await CreateProductAsync(client, isActive: true);
        var recipeId = await CreateRecipeAsync(client);
        var versionId = await CreateVersionAsync(client, recipeId);
        var operationId = await AddOperationAsync(client, versionId);
        await AddOutputAsync(client, operationId, product.Id);
        var validity = await client.PutAsJsonAsync($"/api/recipe-versions/{versionId}/metadata", new
        {
            versionId,
            changeNotes = (string?)null,
            validFrom = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            validTo = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        validity.EnsureSuccessStatusCode();

        // Act: direct API release bypassing the checklist dialog.
        var release = await client.PostAsync($"/api/recipe-versions/{versionId}/release", null);

        // Assert
        release.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await release.Content.ReadAsStringAsync();
        body.Should().Contain("ValidFrom");

        var get = await client.GetAsync($"/api/recipe-versions/{versionId}");
        get.EnsureSuccessStatusCode();
        var detail = await ReadAsync<VersionDetailDto>(get);
        detail.Status.Should().Be((int)DraftStatus);
    }

    [Fact]
    public async Task Release_DraftWithInactiveBomProduct_Returns400()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var product = await CreateProductAsync(client, isActive: true);
        var recipeId = await CreateRecipeAsync(client);
        var versionId = await CreateVersionAsync(client, recipeId);
        var operationId = await AddOperationAsync(client, versionId);
        await AddOutputAsync(client, operationId, product.Id);
        await AddBomItemAsync(client, operationId, product.Id);
        await DeactivateProductAsync(client, product);

        // Act: direct API release bypassing the checklist dialog.
        var release = await client.PostAsync($"/api/recipe-versions/{versionId}/release", null);

        // Assert
        release.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await release.Content.ReadAsStringAsync();
        body.Should().Contain("inactive");
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

    private static async Task<Guid> AddOperationAsync(HttpClient client, Guid versionId)
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
        var created = await ReadAsync<OperationDto>(response);
        return created.Id;
    }

    private static async Task AddOutputAsync(HttpClient client, Guid operationId, Guid productId)
    {
        var response = await client.PostAsJsonAsync($"/api/operations/{operationId}/outputs", new
        {
            operationId,
            productId,
            measureUnitId = (Guid?)null,
            quantity = 1m,
            quantityType = 1,
            outputType = 1,
            preferredWarehouseId = (Guid?)null,
            notes = (string?)null,
            sortIndex = 0
        });
        response.EnsureSuccessStatusCode();
    }

    private static async Task AddBomItemAsync(HttpClient client, Guid operationId, Guid productId)
    {
        var response = await client.PostAsJsonAsync($"/api/operations/{operationId}/bom-items", new
        {
            operationId,
            productId,
            measureUnitId = (Guid?)null,
            quantity = 2m,
            quantityType = 1,
            scrapPercentage = (decimal?)null,
            isOptional = false,
            preferredWarehouseId = (Guid?)null,
            consumptionTiming = 2,
            notes = (string?)null,
            sortIndex = 0
        });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<ProductRecord> CreateProductAsync(HttpClient client, bool isActive)
    {
        var code = $"P-{Guid.NewGuid():N}"[..10];
        var response = await client.PostAsJsonAsync("/api/products", new
        {
            code,
            name = $"Product {code}",
            description = (string?)null,
            ean = (string?)null,
            barcode = (string?)null,
            scanBy = 1,
            isActive,
            productGroupId = (Guid?)null,
            syncId = (string?)null
        });
        response.EnsureSuccessStatusCode();
        return await ReadAsync<ProductRecord>(response);
    }

    private static async Task DeactivateProductAsync(HttpClient client, ProductRecord product)
    {
        var response = await client.PutAsJsonAsync($"/api/products/{product.Id}", new
        {
            id = product.Id,
            code = product.Code,
            name = product.Name,
            description = (string?)null,
            ean = (string?)null,
            barcode = (string?)null,
            scanBy = 1,
            isActive = false,
            productGroupId = (Guid?)null,
            syncId = (string?)null
        });
        response.EnsureSuccessStatusCode();
    }

    private const int DraftStatus = 1;

    private sealed record RecipeDto(Guid Id, string Code, string Name);
    private sealed record VersionDto(Guid Id, Guid RecipeId, int VersionNumber, int Status);
    private sealed record VersionDetailDto(Guid Id, int Status);
    private sealed record OperationDto(Guid Id);
    private sealed record ProductRecord(Guid Id, string Code, string Name);
}
