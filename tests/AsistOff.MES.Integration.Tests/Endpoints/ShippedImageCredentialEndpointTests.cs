using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Shipped-image hygiene for the dev seed credential (issue #364). The
/// Development settings file must stay out of the Docker build context
/// (<c>.dockerignore</c>) and out of the publish output (Gateway csproj), so
/// inspecting the built image finds no hardcoded seed password under
/// <c>/app</c>. The explicitly configured seed password still provisions the
/// dev tenant admin through the real HTTP pipeline.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ShippedImageCredentialEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public void BuildContext_ExcludesDevelopmentSettings()
    {
        // Arrange — the file the image build must never receive.
        var dockerignore = ReadRepoFile(".dockerignore");

        // Assert — explicit exclusion, no credential in the ignore file itself.
        dockerignore.Should().Contain("appsettings.Development.json");
        dockerignore.Should().NotContain("Passw0rd!");
    }

    [Fact]
    public void Dockerfile_RuntimeStageShipsOnlyPublishOutput()
    {
        // Arrange
        var dockerfile = ReadRepoFile("Dockerfile");

        // Assert — runtime copies only the publish output (which omits the
        // Development settings via CopyToPublishDirectory=Never).
        dockerfile.Should().Contain("COPY --from=build");
        dockerfile.Should().Contain("/app/publish");
        dockerfile.Should().NotContain("Passw0rd!");

        var offending = dockerfile
            .Split('\n')
            .Where(line =>
                !line.TrimStart().StartsWith('#') &&
                line.Contains("appsettings.Development.json", StringComparison.Ordinal));
        offending.Should().BeEmpty("Dockerfile must not COPY the Development settings into any image layer");
    }

    [Fact]
    public async Task ExplicitSeedPassword_ProvisionsDevAdminAsync()
    {
        // Arrange — the fixture host sets the seed password explicitly in
        // code (MesWebApplicationFactory), mirroring the documented
        // Seed__Tenants__0__AdminPassword override path.
        using var client = Fixture.CreateClient();
        await AuthCookieHelper.AttachCsrfAsync(client);

        // Act — the seeded admin signs in through the real pipeline.
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new
        {
            email = IntegrationTestData.AdminEmail,
            password = IntegrationTestData.AdminPassword,
        });

        // Assert — happy path: local dev still seeds the dev tenant admin.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorizedAsync()
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/api/shifts");

        // Assert — failure path: anonymous access stays denied.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 12 && directory is not null; i++)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {relativePath} from test output.");
    }
}
