namespace AsistOff.MES.Shared.Infrastructure.Boot;

/// <summary>
/// Outcome of a single boot decision (issue #257). <see cref="Migrated"/> and
/// <see cref="Seeded"/> report what actually ran; <see cref="ServedTraffic"/>
/// is <c>false</c> for the <c>--migrate-only</c> job entrypoint, which applies
/// migrations (and optionally seeders) and then exits without serving traffic.
/// <see cref="Messages"/> carries the exact log lines the host must emit, in
/// order, so production skips are always visible in the log.
/// </summary>
public sealed record BootExecution(
    bool Migrated,
    bool Seeded,
    bool ServedTraffic,
    IReadOnlyList<string> Messages);

/// <summary>
/// Testable coordinator for the production boot gate (issue #257). The Gateway
/// delegates its startup migrate/seed branching here with the real EF Core and
/// <c>ISeeder</c> callbacks; unit tests inject recording callbacks and prove
/// every branch without a database: production defaults skip both steps yet
/// still serve traffic, explicit opt-ins run migrate once (seeders strictly
/// after a successful migrate), and <c>--migrate-only</c> exits without
/// serving. Contradictory flags fail fast via <see cref="BootOptionsValidator"/>
/// before anything runs.
/// </summary>
public static class BootRunner
{
    public const string MigrateOnlyFlag = "--migrate-only";

    public static bool IsMigrateOnly(string[] args) =>
        args.Any(a => string.Equals(a, MigrateOnlyFlag, StringComparison.OrdinalIgnoreCase));

    public static async Task<BootExecution> RunAsync(
        BootOptions options,
        string[] args,
        Func<CancellationToken, Task> migrateAsync,
        Func<CancellationToken, Task<int>> seedAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(migrateAsync);
        ArgumentNullException.ThrowIfNull(seedAsync);

        BootOptionsValidator.Validate(options);

        if (IsMigrateOnly(args))
        {
            await migrateAsync(cancellationToken).ConfigureAwait(false);
            var messages = new List<string>
            {
                "Migrate-only job applied pending EF Core migrations.",
            };

            var seeded = false;
            if (options.RunSeeders)
            {
                var seederCount = await seedAsync(cancellationToken).ConfigureAwait(false);
                seeded = true;
                messages.Add($"Migrate-only job ran {seederCount} seeders (Boot:RunSeeders=true).");
            }
            else
            {
                messages.Add("Migrate-only job skipped seeders (Boot:RunSeeders=false).");
            }

            return new BootExecution(Migrated: true, Seeded: seeded, ServedTraffic: false, Messages: messages);
        }

        var servedMessages = new List<string>();
        var migrated = false;
        if (options.ApplyMigrations)
        {
            await migrateAsync(cancellationToken).ConfigureAwait(false);
            migrated = true;
            servedMessages.Add("Applied pending EF Core migrations on boot (Boot:ApplyMigrations=true).");
        }
        else
        {
            servedMessages.Add(
                "Skipped EF Core migrations on boot (Boot:ApplyMigrations=false). " +
                "Run the migrate job (dotnet AsistOff.MES.Gateway.dll --migrate-only) to upgrade the schema.");
        }

        var ranSeeders = false;
        if (options.RunSeeders)
        {
            var seederCount = await seedAsync(cancellationToken).ConfigureAwait(false);
            ranSeeders = true;
            servedMessages.Add($"Ran {seederCount} seeders on boot (Boot:RunSeeders=true).");
        }
        else
        {
            servedMessages.Add("Skipped seeders on boot (Boot:RunSeeders=false).");
        }

        return new BootExecution(
            Migrated: migrated,
            Seeded: ranSeeders,
            ServedTraffic: true,
            Messages: servedMessages);
    }
}
