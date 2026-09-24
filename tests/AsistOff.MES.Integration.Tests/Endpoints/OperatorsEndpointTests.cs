using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint tests for <c>/api/operators</c> covering issue #88 finding 2:
/// creating an operator without a department must succeed (never a 500).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class OperatorsEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/operators";
    private static readonly Guid EmptyUserId = Guid.Empty;

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            identifier = "NOAUTH",
            firstName = "A",
            lastName = "B",
            ratePerHour = 10m,
            departmentId = (Guid?)null,
            userId = EmptyUserId
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_WithoutDepartment_ReturnsCreated_AndIsRetrievable()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var identifier = $"OP-{Guid.NewGuid():N}"[..12];

        // Act — department omitted entirely.
        var create = await client.PostAsJsonAsync(BaseUrl, new
        {
            identifier,
            firstName = "Jan",
            lastName = "Kowalski",
            ratePerHour = 50m,
            userId = EmptyUserId
        });

        // Assert — must not be a 500 (regression: not-null DepartmentId column).
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<OperatorDto>(create);
        created.Identifier.Should().Be(identifier);

        var get = await client.GetAsync($"{BaseUrl}/{created.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_WithNullDepartment_ReturnsCreated()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var identifier = $"OP-{Guid.NewGuid():N}"[..12];

        // Act
        var create = await client.PostAsJsonAsync(BaseUrl, new
        {
            identifier,
            firstName = "Anna",
            lastName = "Nowak",
            ratePerHour = 60m,
            departmentId = (Guid?)null,
            userId = EmptyUserId
        });

        // Assert
        create.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_WithDepartment_ReturnsCreated()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var departmentId = await CreateDepartmentAsync(client);
        var identifier = $"OP-{Guid.NewGuid():N}"[..12];

        // Act
        var create = await client.PostAsJsonAsync(BaseUrl, new
        {
            identifier,
            firstName = "Ewa",
            lastName = "Zielinska",
            ratePerHour = 70m,
            departmentId,
            userId = EmptyUserId
        });

        // Assert
        create.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_WithEmptyIdentifier_Returns400()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            identifier = "",
            firstName = "Jan",
            lastName = "Kowalski",
            ratePerHour = 10m,
            departmentId = (Guid?)null,
            userId = EmptyUserId
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<Guid> CreateDepartmentAsync(HttpClient client)
    {
        var code = $"D-{Guid.NewGuid():N}"[..10];
        var response = await client.PostAsJsonAsync("/api/departments", new { code, name = $"Dept {code}" });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<DepartmentDto>();
        return created!.Id;
    }

    private sealed record OperatorDto(Guid Id, string Identifier, string FirstName, string LastName, decimal RatePerHour, string? Department);
    private sealed record DepartmentDto(Guid Id, string Code, string Name);
}
