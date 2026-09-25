using AsistOff.MES.Shared.Infrastructure.Boot;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace AsistOff.MES.Shared.Tests.Boot;

/// <summary>
/// Guards the production boot gate from issue #257: with production defaults
/// (no Boot section) boot must not migrate or seed; Development opts back in
/// via appsettings.Development.json; seed-without-migrate fails validation.
/// </summary>
public sealed class BootOptionsTests
{
    [Fact]
    public void Bind_MissingSection_ProductionDefaultsDisableBoth()
    {
        // Arrange — an empty configuration stands in for a production host
        // with no Boot overrides.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        var options = configuration.GetSection(BootOptions.SectionName).Get<BootOptions>()
            ?? new BootOptions();

        // Assert — production-safe defaults: no migrate, no seed.
        options.ApplyMigrations.Should().BeFalse();
        options.RunSeeders.Should().BeFalse();
        var act = () => BootOptionsValidator.Validate(options);
        act.Should().NotThrow();
    }

    [Fact]
    public void Bind_ExplicitMigrateOnly_EnablesMigrationsWithoutSeeders()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Boot:ApplyMigrations"] = "true",
                ["Boot:RunSeeders"] = "false",
            })
            .Build();

        // Act
        var options = configuration.GetSection(BootOptions.SectionName).Get<BootOptions>()!;

        // Assert — each flag opts in independently.
        options.ApplyMigrations.Should().BeTrue();
        options.RunSeeders.Should().BeFalse();
        var act = () => BootOptionsValidator.Validate(options);
        act.Should().NotThrow();
    }

    [Fact]
    public void Bind_ExplicitSeedWithMigrate_EnablesBoth()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Boot:ApplyMigrations"] = "true",
                ["Boot:RunSeeders"] = "true",
            })
            .Build();

        // Act
        var options = configuration.GetSection(BootOptions.SectionName).Get<BootOptions>()!;

        // Assert
        options.ApplyMigrations.Should().BeTrue();
        options.RunSeeders.Should().BeTrue();
        var act = () => BootOptionsValidator.Validate(options);
        act.Should().NotThrow();
    }

    [Fact]
    public void Bind_EnvironmentVariable_EnablesMigrateOverFileDefaults()
    {
        // Arrange — operator opt-in via Boot__ApplyMigrations env var, as the
        // production compose migrate job does.
        const string envName = "Boot__ApplyMigrations";
        Environment.SetEnvironmentVariable(envName, "true");
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Boot:ApplyMigrations"] = "false",
                })
                .AddEnvironmentVariables()
                .Build();

            // Act
            var options = configuration.GetSection(BootOptions.SectionName).Get<BootOptions>()!;

            // Assert
            options.ApplyMigrations.Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, null);
        }
    }

    [Fact]
    public void Bind_BaseAppSettings_ProductionDefaultsDisableBoth()
    {
        // Arrange — the shipped appsettings.json must stay production-safe.
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(GatewaySettingsPath("appsettings.json"), optional: false)
            .Build();

        // Act
        var options = configuration.GetSection(BootOptions.SectionName).Get<BootOptions>()
            ?? new BootOptions();

        // Assert
        options.ApplyMigrations.Should().BeFalse();
        options.RunSeeders.Should().BeFalse();
    }

    [Fact]
    public void Bind_ProductionAppSettings_DisablesBoth()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(GatewaySettingsPath("appsettings.Production.json"), optional: false)
            .Build();

        // Act
        var options = configuration.GetSection(BootOptions.SectionName).Get<BootOptions>()
            ?? new BootOptions();

        // Assert
        options.ApplyMigrations.Should().BeFalse();
        options.RunSeeders.Should().BeFalse();
    }

    [Fact]
    public void Bind_DevelopmentAppSettings_PreservesHistoricalBootBehavior()
    {
        // Arrange — Development keeps migrate + seed on, so local and CI
        // fixture boots behave exactly as before the gate.
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(GatewaySettingsPath("appsettings.Development.json"), optional: false)
            .Build();

        // Act
        var options = configuration.GetSection(BootOptions.SectionName).Get<BootOptions>()
            ?? new BootOptions();

        // Assert
        options.ApplyMigrations.Should().BeTrue();
        options.RunSeeders.Should().BeTrue();
        var act = () => BootOptionsValidator.Validate(options);
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_SeedWithoutMigrate_ThrowsNamingBothFlags()
    {
        // Arrange — seeders assume a migrated schema.
        var options = new BootOptions { ApplyMigrations = false, RunSeeders = true };

        // Act
        var act = () => BootOptionsValidator.Validate(options);

        // Assert — startup must name both offending settings.
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Boot:RunSeeders*Boot:ApplyMigrations*");
    }

    [Fact]
    public void Validate_NullOptions_ThrowsArgumentNull()
    {
        // Arrange
        BootOptions options = null!;

        // Act
        var act = () => BootOptionsValidator.Validate(options);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private static string GatewaySettingsPath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 12 && directory is not null; i++)
        {
            var candidate = Path.Combine(directory.FullName, "AsistOff.MES.Gateway", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate AsistOff.MES.Gateway/{fileName} from test output.");
    }
}
