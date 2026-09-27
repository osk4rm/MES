using AsistOff.MES.Integration.Tests.Infrastructure;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-project regression guard for the Seq authentication surface
/// (issue #377). These are compose-file assertions running in the CI backend
/// job alongside the HTTP contract tests: fresh <c>docker compose up</c> must
/// leave Seq requiring authentication (UI + ingestion), the api containers
/// must authenticate their Serilog sink through an env-only key, and the dev
/// port mapping must bind localhost only. No database or container is needed
/// for the assertions themselves; the collection fixture is shared with the
/// rest of the suite.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class SeqAuthEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public void Compose_SeqService_RequiresAdminPasswordFromEnvironment()
    {
        // Arrange
        var block = ServiceBlock(ReadRepoFile("docker-compose.yml"), "seq");

        // Assert — env-only first-run admin password with fail-fast
        // interpolation (no anonymous Seq UI).
        block.Should().Contain("SEQ_FIRSTRUN_ADMINPASSWORD:");
        block.Should().Contain("${SEQ_ADMIN_PASSWORD:?");
    }

    [Fact]
    public void Compose_SeqService_RequiresAuthenticationForHttpIngestion()
    {
        // Arrange
        var block = ServiceBlock(ReadRepoFile("docker-compose.yml"), "seq");

        // Assert — anonymous ingestion posts are rejected.
        block.Should().Contain("SEQ_FIRSTRUN_REQUIREAUTHENTICATIONFORHTTPINGESTION: \"True\"");
    }

    [Fact]
    public void Compose_SeqService_ShipsNoDefaultCredential()
    {
        // Arrange
        var block = ServiceBlock(ReadRepoFile("docker-compose.yml"), "seq");
        var passwordLine = block
            .Split('\n')
            .Single(l => l.Contains("SEQ_FIRSTRUN_ADMINPASSWORD:", StringComparison.Ordinal));

        // Assert — unset means compose refuses to start, never an open Seq.
        passwordLine.Trim().Should().StartWith("SEQ_FIRSTRUN_ADMINPASSWORD: \"${SEQ_ADMIN_PASSWORD:?");
        block.Should().NotContain("SEQ_ADMIN_PASSWORD:-");
    }

    [Theory]
    [InlineData("api")]
    [InlineData("api-prod")]
    public void Compose_ApiService_AuthenticatesSeqSinkWithEnvKey(string service)
    {
        // Arrange
        var block = ServiceBlock(ReadRepoFile("docker-compose.yml"), service);

        // Assert — the sink still targets the seq container but authenticates
        // with an env-only ingestion API key (never committed).
        block.Should().Contain("Serilog__WriteTo__1__Args__serverUrl: \"http://seq:80\"");
        block.Should().Contain("Serilog__WriteTo__1__Args__apiKey:");
        block.Should().Contain("${SEQ_INGESTION_API_KEY:-}");
    }

    [Fact]
    public void ComposeOverride_SeqPort_BindsLocalhostOnly()
    {
        // Arrange
        var block = ServiceBlock(ReadRepoFile("docker-compose.override.yml"), "seq");

        // Assert — verified via `docker compose config`: the dev Seq port is
        // published on 127.0.0.1 only.
        block.Should().Contain("127.0.0.1:${SEQ_PORT:-5341}:80");
    }

    /// <summary>
    /// Extracts the lines belonging to a two-space-indented compose service
    /// (e.g. <c>  seq:</c>), stopping at the next service or top-level key.
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
