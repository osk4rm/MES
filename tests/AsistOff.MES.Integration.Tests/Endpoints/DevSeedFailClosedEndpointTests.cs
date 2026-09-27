using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;
using AsistOff.MES.Multitenancy.Context;
using AsistOff.MES.Multitenancy.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Fail-closed dev seeding over HTTP (issue #358). The shipped
/// <c>appsettings.Development.json</c> carries no default seed credential, so
/// a boot without an explicit <c>Seed__Tenants__0__AdminPassword</c> value
/// must provision no admin, while the explicitly configured seed password
/// still provisions the dev tenant admin (dev ergonomics preserved).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class DevSeedFailClosedEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public void ShippedDevelopmentSettings_CarryNoDefaultPassword()
    {
        // Arrange — the file the image build excludes from publish output.
        var json = ReadRepoFile(Path.Combine("AsistOff.MES.Gateway", "appsettings.Development.json"));

        // Assert — fail-closed default, no known-default credential shipped.
        json.Should().NotContain("Passw0rd!");
        using var document = JsonDocument.Parse(json);
        var tenants = document.RootElement.GetProperty("Seed").GetProperty("Tenants");
        tenants.GetArrayLength().Should().BeGreaterThan(0);
        foreach (var tenant in tenants.EnumerateArray())
        {
            tenant.GetProperty("AdminPassword").GetString().Should().BeEmpty();
        }
    }

    [Fact]
    public async Task ExplicitSeedPassword_ProvisionsDevAdmin()
    {
        // Arrange — the fixture host sets the seed password explicitly in
        // code (MesWebApplicationFactory), mirroring the documented
        // Seed__Tenants__0__AdminPassword override path.

        // Act — the seeded admin signs in through the real pipeline.
        using var client = Fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new
        {
            email = IntegrationTestData.AdminEmail,
            password = IntegrationTestData.AdminPassword,
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Boot_WithoutExplicitSeedPassword_ProvisionsNoAdmin()
    {
        // Arrange — an isolated host whose only seed entry carries the
        // shipped empty-password default (no explicit override).
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var seedName = $"seed-nopw-{suffix}";
        var contactEmail = $"nopw-{suffix}@integration.local";
        await using var factory = new MesWebApplicationFactory(
            Fixture.PostgresConnectionString,
            configureTestServices: services =>
            {
                services.Configure<DevTenantSeedOptions>(options =>
                {
                    options.Enabled = true;
                    options.Tenants =
                    [
                        new DevTenantSeed
                        {
                            Name = seedName,
                            DisplayName = "No Password Seed",
                            ContactEmail = contactEmail,
                            AdminPassword = string.Empty,
                        },
                    ];
                });
            });

        // Act — boot the host (migrations + seeder skip), then try to use
        // the never-provisioned admin.
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MultitenancyDbContext>();
        var tenantExists = await db.Tenants.AsNoTracking().AnyAsync(t => t.Name == seedName);
        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new
        {
            email = contactEmail,
            password = "S0mething-Strong!",
        });

        // Assert — fail-closed: no tenant row, no session.
        tenantExists.Should().BeFalse();
        signIn.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/api/shifts");

        // Assert
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
