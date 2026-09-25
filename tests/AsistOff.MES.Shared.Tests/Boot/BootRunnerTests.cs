using AsistOff.MES.Shared.Infrastructure.Boot;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Boot;

/// <summary>
/// Behavioral proof for the production boot gate (issue #257, AC1/AC2).
/// <see cref="BootRunner"/> is the exact branching the Gateway executes in
/// <c>Program.cs</c>; recording callbacks stand in for the real EF Core
/// migrate and <c>ISeeder</c> fan-out, so these tests prove skip/migrate/seed
/// ordering, single-application, and the <c>--migrate-only</c> exit without a
/// database.
/// </summary>
public sealed class BootRunnerTests
{
    [Fact]
    public async Task Run_ProductionDefaults_SkipsMigrateAndSeed_ServesTrafficWithSkipLinesAsync()
    {
        // Arrange — production defaults: no migrate, no seed.
        var options = new BootOptions { ApplyMigrations = false, RunSeeders = false };
        var calls = new List<string>();

        // Act — the exact call Program.cs makes, with recording callbacks.
        var execution = await BootRunner.RunAsync(
            options,
            [],
            migrateAsync: _ => { calls.Add("migrate"); return Task.CompletedTask; },
            seedAsync: _ => { calls.Add("seed"); return Task.FromResult(0); });

        // Assert — nothing ran, traffic is served, and both skips are logged.
        calls.Should().BeEmpty();
        execution.Migrated.Should().BeFalse();
        execution.Seeded.Should().BeFalse();
        execution.ServedTraffic.Should().BeTrue();
        execution.Messages.Should().HaveCount(2);
        execution.Messages[0].Should().Contain("Boot:ApplyMigrations=false");
        execution.Messages[1].Should().Contain("Boot:RunSeeders=false");
    }

    [Fact]
    public async Task Run_ExplicitMigrateOnly_AppliesOnceThenServesWithoutSeedingAsync()
    {
        // Arrange
        var options = new BootOptions { ApplyMigrations = true, RunSeeders = false };
        var migrateCalls = 0;
        var seedCalls = 0;

        // Act
        var execution = await BootRunner.RunAsync(
            options,
            [],
            migrateAsync: _ => { migrateCalls++; return Task.CompletedTask; },
            seedAsync: _ => { seedCalls++; return Task.FromResult(0); });

        // Assert — migrations applied exactly once, then traffic is served.
        migrateCalls.Should().Be(1);
        seedCalls.Should().Be(0);
        execution.Migrated.Should().BeTrue();
        execution.Seeded.Should().BeFalse();
        execution.ServedTraffic.Should().BeTrue();
        execution.Messages.Should().Contain(m => m.Contains("Boot:ApplyMigrations=true"));
        execution.Messages.Should().Contain(m => m.Contains("Boot:RunSeeders=false"));
    }

    [Fact]
    public async Task Run_ExplicitMigrateAndSeed_RunsSeedersAfterSuccessfulMigrateAsync()
    {
        // Arrange
        var options = new BootOptions { ApplyMigrations = true, RunSeeders = true };
        var order = new List<string>();

        // Act
        var execution = await BootRunner.RunAsync(
            options,
            [],
            migrateAsync: _ => { order.Add("migrate"); return Task.CompletedTask; },
            seedAsync: _ => { order.Add("seed"); return Task.FromResult(2); });

        // Assert — migrate ran once, seeders ran strictly after it, traffic served.
        order.Should().Equal("migrate", "seed");
        execution.Migrated.Should().BeTrue();
        execution.Seeded.Should().BeTrue();
        execution.ServedTraffic.Should().BeTrue();
        execution.Messages.Should().Contain(m => m.Contains("2 seeders") && m.Contains("Boot:RunSeeders=true"));
    }

    [Fact]
    public async Task Run_MigrateOnlyFlag_AppliesAndExitsWithoutServingAsync()
    {
        // Arrange
        var options = new BootOptions { ApplyMigrations = false, RunSeeders = false };
        var migrateCalls = 0;
        var seedCalls = 0;

        // Act
        var execution = await BootRunner.RunAsync(
            options,
            ["--migrate-only"],
            migrateAsync: _ => { migrateCalls++; return Task.CompletedTask; },
            seedAsync: _ => { seedCalls++; return Task.FromResult(0); });

        // Assert — the job migrates, skips seeders, and never serves traffic.
        migrateCalls.Should().Be(1);
        seedCalls.Should().Be(0);
        execution.Migrated.Should().BeTrue();
        execution.Seeded.Should().BeFalse();
        execution.ServedTraffic.Should().BeFalse();
        execution.Messages.Should().Contain(m => m.Contains("Migrate-only job applied"));
        execution.Messages.Should().Contain(m => m.Contains("Migrate-only job skipped seeders"));
    }

    [Fact]
    public async Task Run_MigrateOnlyWithSeeders_RunsSeedersAfterMigrateThenExitsAsync()
    {
        // Arrange
        var options = new BootOptions { ApplyMigrations = true, RunSeeders = true };
        var order = new List<string>();

        // Act
        var execution = await BootRunner.RunAsync(
            options,
            ["--migrate-only"],
            migrateAsync: _ => { order.Add("migrate"); return Task.CompletedTask; },
            seedAsync: _ => { order.Add("seed"); return Task.FromResult(3); });

        // Assert — seeders run after migrate inside the job; still no traffic.
        order.Should().Equal("migrate", "seed");
        execution.Migrated.Should().BeTrue();
        execution.Seeded.Should().BeTrue();
        execution.ServedTraffic.Should().BeFalse();
        execution.Messages.Should().Contain(m => m.Contains("3 seeders") && m.Contains("Migrate-only job ran"));
    }

    [Fact]
    public void IsMigrateOnly_MatchesFlagCaseInsensitively()
    {
        BootRunner.IsMigrateOnly(["--migrate-only"]).Should().BeTrue();
        BootRunner.IsMigrateOnly(["--MIGRATE-ONLY"]).Should().BeTrue();
        BootRunner.IsMigrateOnly([]).Should().BeFalse();
        BootRunner.IsMigrateOnly(["--other"]).Should().BeFalse();
    }

    [Fact]
    public async Task Run_SeedWithoutMigrate_FailsFastBeforeAnythingRunsAsync()
    {
        // Arrange — seeders assume a migrated schema.
        var options = new BootOptions { ApplyMigrations = false, RunSeeders = true };
        var calls = new List<string>();

        // Act
        var act = () => BootRunner.RunAsync(
            options,
            [],
            migrateAsync: _ => { calls.Add("migrate"); return Task.CompletedTask; },
            seedAsync: _ => { calls.Add("seed"); return Task.FromResult(0); });

        // Assert — validation fires before either callback runs.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Boot:RunSeeders*Boot:ApplyMigrations*");
        calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_MigrateOnlySeedWithoutMigrate_FailsFastAsync()
    {
        // Arrange — the fail-fast rule also guards the job entrypoint.
        var options = new BootOptions { ApplyMigrations = false, RunSeeders = true };
        var migrateCalls = 0;

        // Act
        var act = () => BootRunner.RunAsync(
            options,
            ["--migrate-only"],
            migrateAsync: _ => { migrateCalls++; return Task.CompletedTask; },
            seedAsync: _ => Task.FromResult(0));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        migrateCalls.Should().Be(0);
    }

    [Fact]
    public async Task Run_NullOptions_ThrowsArgumentNullAsync()
    {
        // Act
        var act = () => BootRunner.RunAsync(
            null!,
            [],
            migrateAsync: _ => Task.CompletedTask,
            seedAsync: _ => Task.FromResult(0));

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
