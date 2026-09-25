namespace AsistOff.MES.Shared.Infrastructure.Boot;

/// <summary>
/// Startup gate for EF Core migrations and <c>ISeeder</c> execution (issue #257).
/// Production defaults are safe: both flags default to <c>false</c>, so a host
/// booted without explicit configuration never migrates the plant database and
/// never runs seeders. Development opts back into the historical behavior via
/// <c>appsettings.Development.json</c> (<c>true</c>/<c>true</c>).
/// Binds from the <c>Boot</c> configuration section; operators can also flip
/// each flag independently with <c>Boot__ApplyMigrations</c> /
/// <c>Boot__RunSeeders</c> environment variables.
/// </summary>
public sealed class BootOptions
{
    public const string SectionName = "Boot";

    /// <summary>
    /// When <c>true</c>, boot applies all pending EF Core migrations before
    /// serving traffic. Defaults to <c>false</c> (production-safe).
    /// </summary>
    public bool ApplyMigrations { get; set; }

    /// <summary>
    /// When <c>true</c>, boot runs every registered <c>ISeeder</c> after a
    /// successful migrate. Requires <see cref="ApplyMigrations"/> (enforced by
    /// <see cref="BootOptionsValidator"/>). Defaults to <c>false</c>.
    /// </summary>
    public bool RunSeeders { get; set; }
}
