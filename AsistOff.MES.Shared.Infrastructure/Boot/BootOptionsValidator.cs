namespace AsistOff.MES.Shared.Infrastructure.Boot;

/// <summary>
/// Startup guard for <see cref="BootOptions"/>. Seeders assume a migrated
/// schema, so requesting seeders without migrations is a fail-fast
/// configuration error rather than a half-seeded production database.
/// </summary>
public static class BootOptionsValidator
{
    public static void Validate(BootOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.RunSeeders && !options.ApplyMigrations)
        {
            throw new InvalidOperationException(
                "Boot:RunSeeders requires Boot:ApplyMigrations to be true: seeders assume a migrated schema. " +
                "Either set Boot:ApplyMigrations=true (or run the migrate job first) or set Boot:RunSeeders=false.");
        }
    }
}
