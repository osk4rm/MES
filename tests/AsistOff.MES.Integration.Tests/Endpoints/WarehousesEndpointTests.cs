using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint tests for <c>/api/warehouses</c> covering issue #88 finding 1:
/// creating a warehouse without SyncId must succeed (SyncId is optional end-to-end).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class WarehousesEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/warehouses";

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.PostAsJsonAsync(BaseUrl, new { name = "NoAuth" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_WithoutSyncId_ReturnsCreated_AndIsRetrievable()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var name = $"WH-{Guid.NewGuid():N}"[..12];

        // Act — omit syncId entirely, like the UI dialog does.
        var create = await client.PostAsJsonAsync(BaseUrl, new { name });

        // Assert
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<WarehouseDto>(create);
        created.Name.Should().Be(name);
        created.SyncId.Should().BeNull();

        var get = await client.GetAsync($"{BaseUrl}/{created.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_WithNullSyncId_ReturnsCreated()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var name = $"WH-{Guid.NewGuid():N}"[..12];

        // Act
        var create = await client.PostAsJsonAsync(BaseUrl, new { name, syncId = (string?)null });

        // Assert
        create.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_WithEmptyName_Returns400()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.PostAsJsonAsync(BaseUrl, new { name = "  " });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record WarehouseDto(Guid Id, string Name, string? SyncId);
}
