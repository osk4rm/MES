using System.Text.Json;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Multitenancy;

/// <summary>
/// Shipped-image proof for the dev seed credential (issues #358, #364). The
/// Development settings file must carry no hardcoded admin password (empty
/// default so the seeder skips fail-closed), the Gateway project must exclude
/// it from publish output, the Docker build context must exclude it via
/// <c>.dockerignore</c> so no shipped layer contains it, and the local compose
/// flow must supply the password explicitly via
/// <c>DEV_SEED_ADMIN_PASSWORD</c> instead of a checked-in default.
/// </summary>
public sealed class DevSeedCredentialTests
{
    [Fact]
    public void DevelopmentSettings_ContainNoHardcodedSeedPassword()
    {
        // Arrange
        var json = ReadRepoFile(Path.Combine("AsistOff.MES.Gateway", "appsettings.Development.json"));

        // Assert — no known-default credential anywhere in the shipped file.
        json.Should().NotContain("Passw0rd!");
    }

    [Fact]
    public void DevelopmentSettings_ShipEmptySeedAdminPassword()
    {
        // Arrange
        var json = ReadRepoFile(Path.Combine("AsistOff.MES.Gateway", "appsettings.Development.json"));

        // Act
        using var document = JsonDocument.Parse(json);
        var tenants = document.RootElement.GetProperty("Seed").GetProperty("Tenants");

        // Assert — every shipped seed entry is fail-closed (empty password).
        tenants.GetArrayLength().Should().BeGreaterThan(0);
        foreach (var tenant in tenants.EnumerateArray())
        {
            tenant.GetProperty("AdminPassword").GetString().Should().BeEmpty();
        }
    }

    [Fact]
    public void GatewayProject_ExcludesDevelopmentSettingsFromPublish()
    {
        // Arrange
        var csproj = ReadRepoFile(Path.Combine("AsistOff.MES.Gateway", "AsistOff.MES.Gateway.csproj"));

        // Assert — publish output never contains the Development settings file.
        csproj.Should().Contain("appsettings.Development.json");
        csproj.Should().Contain("<CopyToPublishDirectory>Never</CopyToPublishDirectory>");
        csproj.Should().NotContain("Passw0rd!");
    }

    [Fact]
    public void ComposeOverride_SuppliesSeedPasswordFromEnvWithoutDefault()
    {
        // Arrange
        var block = ServiceBlock(ReadRepoFile("docker-compose.override.yml"), "api");

        // Assert — the local flow passes the password through explicitly and
        // carries no hardcoded fallback credential.
        block.Should().Contain("Seed__Tenants__0__AdminPassword");
        block.Should().Contain("DEV_SEED_ADMIN_PASSWORD");
        block.Should().NotContain("Passw0rd!");
    }

    [Fact]
    public void EnvExample_DocumentsSeedPasswordWithoutHardcodedValue()
    {
        // Arrange
        var env = ReadRepoFile(".env.example");

        // Assert
        env.Should().Contain("DEV_SEED_ADMIN_PASSWORD=");
        env.Should().NotContain("Passw0rd!");
    }

    [Fact]
    public void Dockerignore_ExcludesDevelopmentSettingsFromBuildContext()
    {
        // Arrange — issue #364: the Development settings file must never enter
        // the Docker build context, so no shipped layer can contain it.
        var dockerignore = ReadRepoFile(".dockerignore");

        // Assert — an explicit exclusion pattern covers the Gateway file, and
        // the ignore file itself carries no credential.
        dockerignore.Should().Contain("appsettings.Development.json");
        dockerignore.Should().NotContain("Passw0rd!");
    }

    [Fact]
    public void Dockerfile_RuntimeStageShipsOnlyPublishOutput()
    {
        // Arrange — issue #364: the runtime stage must copy only the publish
        // output (which already omits the Development settings via the csproj
        // CopyToPublishDirectory=Never rule); no build instruction may copy
        // the Development settings file into the image directly.
        var dockerfile = ReadRepoFile("Dockerfile");

        // Assert — runtime copies the publish directory.
        dockerfile.Should().Contain("COPY --from=build");
        dockerfile.Should().Contain("/app/publish");
        dockerfile.Should().NotContain("Passw0rd!");

        // No non-comment instruction references the Development settings file
        // as a copy source (explanatory comments mentioning it are allowed).
        var offending = dockerfile
            .Split('\n')
            .Where(line =>
                !line.TrimStart().StartsWith('#') &&
                line.Contains("appsettings.Development.json", StringComparison.Ordinal));
        offending.Should().BeEmpty("Dockerfile must not COPY the Development settings into any image layer");
    }

    /// <summary>
    /// Extracts the lines belonging to a two-space-indented compose service
    /// (e.g. <c>  api:</c>), stopping at the next service or top-level key.
    /// </summary>
    private static string ServiceBlock(string compose, string serviceName)
    {
        var lines = compose.Split('\n');
        var start = Array.FindIndex(
            lines,
            l => l.StartsWith($"  {serviceName}:", StringComparison.Ordinal));
        start.Should().BeGreaterThanOrEqualTo(
            0, $"docker-compose.override.yml must define a '{serviceName}:' service");

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
