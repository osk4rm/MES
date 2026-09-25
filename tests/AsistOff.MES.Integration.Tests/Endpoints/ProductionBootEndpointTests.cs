using System.Net;
using System.Text.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Production boot safety (issue #257). The shared fixture boots exactly like
/// a Development host, so signing in with the dev-seeded tenant proves the
/// Boot gate still migrates and seeds in Development; the block-scoped compose
/// assertions prove the migrate job profiles/command/env, the production
/// gateway <c>service_completed_successfully</c> dependency, and the nightly
/// backup service (image/volume/command plus retention/schedule envs); the
/// backup-script and runbook assertions prove the remaining ops surface.
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
    public void ComposeFile_MigrateJob_HasProfilesCommandAndEnv()
    {
        // Arrange
        var block = ServiceBlock(ReadRepoFile("docker-compose.yml"), "migrate");

        // Assert — one-shot job in both profiles, running the job entrypoint
        // with migrations forced on and no restart.
        block.Should().Contain("profiles:");
        block.Should().Contain("\"migrate\"");
        block.Should().Contain("\"production\"");
        block.Should().Contain("--migrate-only");
        block.Should().Contain("Boot__ApplyMigrations");
        block.Should().Contain("restart: \"no\"");
    }

    [Fact]
    public void ComposeFile_ApiProd_WaitsForMigrateSuccessAndNeverMigratesItself()
    {
        // Arrange
        var block = ServiceBlock(ReadRepoFile("docker-compose.yml"), "api-prod");

        // Assert — the production gateway keeps both boot flags off and only
        // starts after the migrate job completes successfully.
        block.Should().Contain("\"production\"");
        block.Should().Contain("Boot__ApplyMigrations: \"false\"");
        block.Should().Contain("Boot__RunSeeders: \"false\"");
        var dependsOn = block.Substring(block.IndexOf("depends_on:", StringComparison.Ordinal));
        dependsOn.Should().Contain("migrate:");
        dependsOn.Should().Contain("service_completed_successfully");
    }

    [Fact]
    public void ComposeFile_BackupService_DumpsToVolumeWithRetentionAndSchedule()
    {
        // Arrange
        var block = ServiceBlock(ReadRepoFile("docker-compose.yml"), "backup");

        // Assert — postgres image runs backup.sh into the pg_backups volume
        // on the documented schedule/retention env vars.
        block.Should().Contain("image: postgres");
        block.Should().Contain("/backup.sh");
        block.Should().Contain("pg_backups:/backups");
        block.Should().Contain("BACKUP_RETENTION_COUNT");
        block.Should().Contain("BACKUP_INTERVAL_SECONDS");
        block.Should().Contain("pg_backups");
    }

    [Fact]
    public void BackupScript_DumpsTimestampedFileAndPrunesBeyondRetention()
    {
        // Arrange
        var script = ReadRepoFile(Path.Combine("docker", "backup", "backup.sh"));

        // Assert — custom-format pg_dump with a sortable timestamp, retention
        // pruning keyed to BACKUP_RETENTION_COUNT, and a clear password guard.
        script.Should().Contain("pg_dump");
        script.Should().Contain("-F c");
        script.Should().Contain("date +%Y%m%d-%H%M%S");
        script.Should().Contain("/backups/mes_");
        script.Should().Contain("BACKUP_RETENTION_COUNT");
        script.Should().Contain("BACKUP_INTERVAL_SECONDS");
        script.Should().Contain("PGPASSWORD");
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

    [Fact]
    public void Runbook_DocumentsMigrateSeedBackupAndRestore()
    {
        // Arrange
        var runbook = ReadRepoFile(Path.Combine("docs", "production-runbook.md"));

        // Assert — one section per operator workflow with the exact commands.
        runbook.Should().Contain("## Migrate");
        runbook.Should().Contain("## Seed");
        runbook.Should().Contain("## Backup");
        runbook.Should().Contain("## Restore");
        runbook.Should().Contain("--profile production");
        runbook.Should().Contain("--profile migrate run --rm migrate");
        runbook.Should().Contain("pg_restore");
        runbook.Should().Contain("BACKUP_RETENTION_COUNT");
    }

    /// <summary>
    /// Extracts the lines belonging to a two-space-indented compose service
    /// (e.g. <c>  migrate:</c>), stopping at the next service or top-level key.
    /// </summary>
    private static string ServiceBlock(string compose, string serviceName)
    {
        var lines = compose.Split('\n');
        var start = Array.FindIndex(
            lines,
            l => l.StartsWith($"  {serviceName}:", StringComparison.Ordinal));
        start.Should().BeGreaterThanOrEqualTo(
            0, $"docker-compose.yml must define a '{serviceName}:' service");

        var taken = new List<string> { lines[start] };
        for (var i = start + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Length == 0 || line.StartsWith('#'))
            {
                taken.Add(line);
                continue;
            }

            var indent = line.Length - line.TrimStart().Length;
            if (indent <= 2 && line.Trim().Length > 0)
            {
                break;
            }

            taken.Add(line);
        }

        return string.Join('\n', taken);
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
