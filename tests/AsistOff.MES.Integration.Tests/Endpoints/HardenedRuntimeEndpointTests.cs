using System.Net;
using AsistOff.MES.Integration.Tests.Infrastructure;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped tests for the hardened container defaults (issue #271).
/// The health probes must stay green on the hardened configuration path, and
/// the compose/Dockerfile surface must keep its pins, non-root users,
/// restart policies, resource limits, runtime URL wiring, and fail-fast
/// secrets (no weak defaults). File assertions are scoped to the owning
/// service block so a passing test proves the setting sits on the right
/// service, not just anywhere in the file.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class HardenedRuntimeEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private static readonly string[] LongLivedServices =
    [
        "postgres", "seq", "api", "web", "api-prod", "backup", "swarm",
    ];

    [Fact]
    public async Task Live_Returns200_OnHardenedConfigurationPath()
    {
        // Arrange - anonymous client, no token.
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ready_Returns200_OnHardenedConfigurationPath()
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/health/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Healthy");
    }

    [Theory]
    [InlineData("postgres")]
    [InlineData("seq")]
    [InlineData("api")]
    [InlineData("web")]
    [InlineData("api-prod")]
    [InlineData("backup")]
    [InlineData("swarm")]
    public void Compose_LongLivedService_HasRestartPolicyAndResourceLimits(string service)
    {
        // Arrange
        var block = ServiceBlock(ReadRepoFile("docker-compose.yml"), service);

        // Act + Assert — restart policy plus memory and CPU limits on the
        // owning service block.
        block.Should().Contain("restart:");
        block.Should().Contain("memory:");
        block.Should().Contain("cpus:");
    }

    [Fact]
    public void Compose_MigrateJob_StaysOneShotWithoutLimits()
    {
        // Arrange — the one-shot migrate job must not restart or hold limits.
        var block = ServiceBlock(ReadRepoFile("docker-compose.yml"), "migrate");

        // Assert
        block.Should().Contain("restart: \"no\"");
        block.Should().Contain("--migrate-only");
    }

    [Fact]
    public void Compose_DefinesNoWeakDefaultSecrets()
    {
        // Arrange
        var compose = ReadRepoFile("docker-compose.yml");

        // Assert — the historical `:-root` fallbacks are gone; required
        // secrets fail fast with a generation hint instead.
        compose.Should().NotContain(":-root");
        compose.Should().Contain("POSTGRES_PASSWORD:?");
        compose.Should().Contain("AUTH_ISSUER_SIGNING_KEY:?");
    }

    [Fact]
    public void Compose_WebService_ResolvesApiUrlAtRuntime()
    {
        // Arrange
        var block = ServiceBlock(ReadRepoFile("docker-compose.yml"), "web");

        // Assert — runtime override plus the build-time fallback.
        block.Should().Contain("API_BASE_URL");
        block.Should().Contain("VITE_API_BASE_URL");
        block.Should().Contain("3000:8080");
    }

    [Fact]
    public void Dockerfiles_PinEveryBaseImageByDigest()
    {
        // Arrange
        var apiDockerfile = ReadRepoFile("Dockerfile");
        var webDockerfile = ReadRepoFile(Path.Combine("AsistOff.MES.Web", "Dockerfile"));

        // Act + Assert — every FROM line pins image:tag@sha256:<digest>.
        foreach (var dockerfile in new[] { apiDockerfile, webDockerfile })
        {
            var fromLines = dockerfile
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.StartsWith("FROM ", StringComparison.OrdinalIgnoreCase))
                .ToList();

            fromLines.Should().NotBeEmpty();
            foreach (var from in fromLines)
            {
                from.Should().Contain("@sha256:");
            }
        }
    }

    [Fact]
    public void Dockerfiles_RunRuntimeStageAsNonRootUser()
    {
        // Arrange
        var apiDockerfile = ReadRepoFile("Dockerfile");
        var webDockerfile = ReadRepoFile(Path.Combine("AsistOff.MES.Web", "Dockerfile"));

        // Assert — `docker exec <svc> id -u` reports a non-zero UID.
        apiDockerfile.Should().Contain("USER app");
        webDockerfile.Should().Contain("USER nginx");
    }

    [Fact]
    public void WebEntrypoint_ResolvesRuntimeUrlWithDefaultAndFailFast()
    {
        // Arrange
        var entrypoint = ReadRepoFile(Path.Combine("AsistOff.MES.Web", "docker-entrypoint.sh"));

        // Assert — env-over-default precedence, /config.js rendering, and the
        // fail-fast message when the value is emptied.
        entrypoint.Should().Contain("API_BASE_URL");
        entrypoint.Should().Contain("http://localhost:8080");
        entrypoint.Should().Contain("/config.js");
        entrypoint.Should().Contain("not configured");
    }

    [Fact]
    public void Compose_PinsDeployedImagesByDigest()
    {
        // Arrange
        var compose = ReadRepoFile("docker-compose.yml");

        // Assert — prebuilt images carry the same digest pinning as Dockerfiles.
        compose.Should().Contain("postgres:16-alpine@sha256:");
        compose.Should().Contain("datalust/seq:2024.3@sha256:");
    }

    /// <summary>
    /// Extracts the lines belonging to a two-space-indented compose service
    /// (e.g. <c>  web:</c>), stopping at the next service or top-level key.
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
