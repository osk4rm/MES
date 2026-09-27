using System.Net;
using AsistOff.MES.Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Startup wiring guard for issue #329: the production host must register every
/// policy declared by every discovered <c>IModule.Policies</c> entry, and the
/// authorization pipeline must keep working (authenticated browse succeeds,
/// anonymous browse is still 401).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ModulePolicyRegistrationEndpointTests(MesApplicationFixture fixture)
    : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task DeclaredModulePolicies_ResolveFromPolicyProvider()
    {
        // Arrange — the real host booted by the collection fixture.

        // Act
        using var scope = Fixture.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>();

        // Assert — every shipped module policy resolves; unknown policies fail closed.
        foreach (var policyName in new[] { "configuration", "production", "attachments" })
        {
            var policy = await provider.GetPolicyAsync(policyName);
            policy.Should().NotBeNull($"module policy '{policyName}' must be registered at startup");
        }

        (await provider.GetPolicyAsync("no-such-policy")).Should().BeNull();
    }

    [Fact]
    public async Task BrowseProducts_Authenticated_ReturnsOk()
    {
        // Arrange — proves no policy misregistration broke the pipeline.
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/api/products?page=1&pageSize=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task BrowseProducts_WithoutToken_Returns401()
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/api/products?page=1&pageSize=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
