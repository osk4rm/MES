using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Proves the multi-tenancy contract at the HTTP boundary: data written under
/// one tenant must be invisible to another, courtesy of the JWT tenant claim
/// plus the EF Core global query filter.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class TenantIsolationEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task ReasonCode_CreatedInAnotherTenant_IsNotVisible()
    {
        // Arrange - provision a second tenant and create a reason code there
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);

        var code = $"ISO-{Guid.NewGuid():N}"[..12];
        var createResponse = await otherTenantClient.PostAsJsonAsync("/api/reason-codes", new
        {
            code,
            name = "Other tenant only",
            description = (string?)null,
            category = 1,
            isActive = true,
            sortIndex = 0
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act - browse for the same code as the seeded dev tenant
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var browseResponse = await devClient.GetAsync($"/api/reason-codes?code={code}");

        // Assert - the other tenant's reason code is filtered out
        browseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<ReasonCodeDto>>(browseResponse);
        page.Items.Should().BeEmpty();
    }
}
