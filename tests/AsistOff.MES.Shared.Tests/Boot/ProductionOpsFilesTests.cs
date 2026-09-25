using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Boot;

/// <summary>
/// Structured proof for the production ops surface (issue #257, AC3/AC4/AC5).
/// Unlike whole-file substring checks, every compose assertion is scoped to
/// the owning service block (two-space YAML section), so it proves the
/// migrate profiles/command/env, the <c>api-prod -&gt; migrate</c>
/// <c>service_completed_successfully</c> edge, and the backup image/volume/
/// command plus retention envs — plus the <c>backup.sh</c> pg_dump/retention
/// logic and the runbook migrate/seed/backup/restore sections.
/// </summary>
public sealed class ProductionOpsFilesTests
{
    [Fact]
    public void Compose_MigrateJob_HasProfilesCommandAndEnv()
    {
        // Arrange
        var block = ServiceBlock(ReadRepoFile("docker-compose.yml"), "migrate");

        // Assert — one-shot job in both profiles, running the job entrypoint
        // with migrations forced on.
        block.Should().Contain("profiles:");
        block.Should().Contain("\"migrate\"");
        block.Should().Contain("\"production\"");
        block.Should().Contain("--migrate-only");
        block.Should().Contain("Boot__ApplyMigrations");
        block.Should().Contain("\"true\"");
        block.Should().Contain("restart: \"no\"");
    }

    [Fact]
    public void Compose_ApiProd_WaitsForMigrateSuccessAndNeverMigratesItself()
    {
        // Arrange
        var block = ServiceBlock(ReadRepoFile("docker-compose.yml"), "api-prod");

        // Assert — production gateway joins only the production profile,
        // keeps both boot flags off, and starts after the migrate job
        // completes successfully.
        block.Should().Contain("\"production\"");
        block.Should().Contain("Boot__ApplyMigrations: \"false\"");
        block.Should().Contain("Boot__RunSeeders: \"false\"");
        var dependsOn = block.Substring(block.IndexOf("depends_on:", StringComparison.Ordinal));
        dependsOn.Should().Contain("migrate:");
        dependsOn.Should().Contain("service_completed_successfully");
    }

    [Fact]
    public void Compose_BackupService_DumpsToVolumeWithRetentionAndSchedule()
    {
        // Arrange
        var block = ServiceBlock(ReadRepoFile("docker-compose.yml"), "backup");

        // Assert — postgres image runs backup.sh, persists to the pg_backups
        // volume, and honors the schedule + retention env vars.
        block.Should().Contain("image: postgres");
        block.Should().Contain("/backup.sh");
        block.Should().Contain("pg_backups:/backups");
        block.Should().Contain("backup.sh:/backup.sh");
        block.Should().Contain("BACKUP_INTERVAL_SECONDS");
        block.Should().Contain("BACKUP_RETENTION_COUNT");
        block.Should().Contain("\"production\"");
        block.Should().Contain("\"backup\"");
    }

    [Fact]
    public void Compose_TopLevelVolumes_DeclaresPgBackups()
    {
        // Arrange
        var compose = ReadRepoFile("docker-compose.yml");
        var volumes = TopLevelBlock(compose, "volumes:");

        // Assert
        volumes.Should().Contain("pg_backups:");
    }

    [Fact]
    public void BackupScript_DumpsTimestampedFileAndPrunesBeyondRetention()
    {
        // Arrange
        var script = ReadRepoFile(Path.Combine("docker", "backup", "backup.sh"));

        // Assert — custom-format pg_dump to /backups with a sortable
        // timestamp, retention pruning keyed to BACKUP_RETENTION_COUNT,
        // strict mode, and a clear failure when the password is missing.
        script.Should().Contain("pg_dump");
        script.Should().Contain("-F c");
        script.Should().Contain("date +%Y%m%d-%H%M%S");
        script.Should().Contain("/backups/mes_");
        script.Should().Contain("BACKUP_RETENTION_COUNT");
        script.Should().Contain("BACKUP_INTERVAL_SECONDS");
        script.Should().Contain("set -eu");
        script.Should().Contain("PGPASSWORD");
    }

    [Fact]
    public void Runbook_DocumentsMigrateSeedBackupAndRestore()
    {
        // Arrange
        var runbook = ReadRepoFile(Path.Combine("docs", "production-runbook.md"));

        // Assert — one section per operator workflow, with the exact
        // commands and env vars from the acceptance criteria.
        runbook.Should().Contain("## Migrate");
        runbook.Should().Contain("## Seed");
        runbook.Should().Contain("## Backup");
        runbook.Should().Contain("## Restore");
        runbook.Should().Contain("--profile production");
        runbook.Should().Contain("--profile migrate run --rm migrate");
        runbook.Should().Contain("pg_restore");
        runbook.Should().Contain("BACKUP_RETENTION_COUNT");
        runbook.Should().Contain("BACKUP_INTERVAL_SECONDS");
        runbook.Should().Contain("Boot__ApplyMigrations");
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

    private static string TopLevelBlock(string compose, string key)
    {
        var lines = compose.Split('\n');
        var start = Array.FindIndex(
            lines,
            l => l.StartsWith(key, StringComparison.Ordinal));
        start.Should().BeGreaterThanOrEqualTo(
            0, $"docker-compose.yml must define a top-level '{key}' block");

        var taken = new List<string> { lines[start] };
        for (var i = start + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Length > 0 && line[0] != ' ' && line[0] != '\t' && line.Trim().Length > 0)
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
