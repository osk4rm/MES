using System.Net;
using System.Text.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Production boot safety (issue #257). The shared fixture boots exactly like
/// a Development host, so signing in with the dev-seeded tenant proves the
/// Boot gate still migrates and seeds in Development; the compose-file and
/// production-settings assertions prove the migrate job, the production
/// gateway dependency, and the nightly backup service exist with the
/// documented env vars.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ProductionBootEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task DevelopmentFixtureBoot_MigratesAndSeedsDevTenant()
    {
        // Arrange — no setup: the fixture host already booted with
        // Boot:ApplyMigrations/RunSeeders from appsettings.Development.json.

        // Act — the dev tenant admin only exists when migrations ran and the
        // dev seeder provisioned the tenant through the real pipeline; the
        // follow-up browse proves tenant resolution works on the migrated schema.
        using var client = await Fixture.CreateAuthenticatedClientAsync(
            IntegrationTestData.AdminEmail, IntegrationTestData.AdminPassword);
        var shifts = await client.GetAsync("/api/shifts");

        // Assert
        shifts.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void ComposeFile_DefinesMigrateJob_BeforeGatewayStart()
    {
        // Arrange
        var compose = ReadRepoFile("docker-compose.yml");

        // Assert — migrate job entrypoint plus the production gateway gate.
        compose.Should().Contain("migrate:");
        compose.Should().Contain("--migrate-only");
        compose.Should().Contain("Boot__ApplyMigrations");
        compose.Should().Contain("service_completed_successfully");
    }

    [Fact]
    public void ComposeFile_DefinesBackupService_WithRetentionAndSchedule()
    {
        // Arrange
        var compose = ReadRepoFile("docker-compose.yml");

        // Assert — nightly pg_dump example honoring the documented env vars.
        compose.Should().Contain("backup:");
        compose.Should().Contain("pg_dump");
        compose.Should().Contain("BACKUP_RETENTION_COUNT");
        compose.Should().Contain("BACKUP_INTERVAL_SECONDS");
        compose.Should().Contain("pg_backups");
    }

    [Fact]
    public void ProductionSettings_DisableBootByDefault()
    {
        // Arrange
        var json = ReadRepoFile(Path.Combine("AsistOff.MES.Gateway", "appsettings.Production.json"));

        // Act
        using var document = JsonDocument.Parse(json);
        var boot = document.RootElement.GetProperty("Boot");

        // Assert — a production host with no overrides migrates and seeds nothing.
        boot.GetProperty("ApplyMigrations").GetBoolean().Should().BeFalse();
        boot.GetProperty("RunSeeders").GetBoolean().Should().BeFalse();
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
