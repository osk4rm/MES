using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Boot;

/// <summary>
/// Structured proof for the Seq authentication surface (issue #377).
/// Unlike whole-file substring checks, every compose assertion is scoped to
/// the owning service block (two-space YAML section), so it proves the admin
/// credential is env-only with fail-fast interpolation on the <c>seq</c>
/// service, the Serilog sink API key is env-only on <c>api</c>/<c>api-prod</c>,
/// and the dev port binding is localhost-only in the override file — plus the
/// <c>.env.example</c> / <c>generate-env.sh</c> / <c>docs/deployment.md</c>
/// operator wiring that makes the variables discoverable.
/// </summary>
public sealed class SeqAuthOpsFilesTests
{
    [Fact]
    public void Compose_SeqService_RequiresAdminPasswordFromEnvironment()
    {
        // Arrange
        var block = ServiceBlock(ReadRepoFile("docker-compose.yml"), "seq");

        // Assert — first-run admin password is env-only (`:?` fails fast at
        // `docker compose config` time when SEQ_ADMIN_PASSWORD is unset, with
        // a generation hint instead of a default credential).
        block.Should().Contain("SEQ_FIRSTRUN_ADMINPASSWORD:");
        block.Should().Contain("${SEQ_ADMIN_PASSWORD:?");
        block.Should().Contain("generate-env.sh");
    }

    [Fact]
    public void Compose_SeqService_RequiresAuthenticationForHttpIngestion()
    {
        // Arrange
        var block = ServiceBlock(ReadRepoFile("docker-compose.yml"), "seq");

        // Assert — anonymous log ingestion posts are rejected, so the
        // api/api-prod sink must present the ingestion API key.
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

        // Assert — no `:-fallback` default for the admin password: unset
        // means compose refuses to start, never an open Seq with a known
        // password. The value must be exactly the fail-fast interpolation.
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

        // Assert — logs still ship to the seq container over the docker
        // network, but the sink authenticates with an env-only ingestion API
        // key (empty until the operator provisions it in the Seq UI; Seq
        // rejects the anonymous posts in the meantime — fail-closed).
        block.Should().Contain("Serilog__WriteTo__1__Args__serverUrl: \"http://seq:80\"");
        block.Should().Contain("Serilog__WriteTo__1__Args__apiKey:");
        block.Should().Contain("${SEQ_INGESTION_API_KEY:-}");
        block.Should().NotContain("SEQ_INGESTION_API_KEY:?");
    }

    [Fact]
    public void ComposeOverride_SeqPort_BindsLocalhostOnly()
    {
        // Arrange — the dev port mapping lives in the local override (Coolify
        // runs with an explicit `-f docker-compose.yml` and ignores it).
        var block = ServiceBlock(ReadRepoFile("docker-compose.override.yml"), "seq");

        // Assert — the published Seq port binds 127.0.0.1: the log browser
        // stays convenient without exposing full application logs to the LAN.
        block.Should().Contain("127.0.0.1:${SEQ_PORT:-5341}:80");
    }

    [Fact]
    public void EnvExample_DocumentsSeqAuthVariables()
    {
        // Arrange
        var envExample = ReadRepoFile(".env.example");

        // Assert — every new variable is documented with its bootstrap flow;
        // placeholders stay empty (values come from .env, never the repo).
        envExample.Should().Contain("SEQ_ADMIN_PASSWORD=");
        envExample.Should().Contain("SEQ_INGESTION_API_KEY=");
        envExample.Should().Contain("SEQ_PORT=5341");
        envExample.Should().Contain("generate-env.sh");
        envExample.Should().Contain("API Keys");
    }

    [Fact]
    public void GenerateEnvScript_FillsSeqAdminPassword()
    {
        // Arrange
        var script = ReadRepoFile(Path.Combine("scripts", "generate-env.sh"));

        // Assert — first boot mints the Seq admin password like the other
        // required secrets; the ingestion key stays manual (it is
        // provisioned in the Seq UI after boot, see .env.example).
        script.Should().Contain("fill_if_empty \"SEQ_ADMIN_PASSWORD\"");
        script.Should().NotContain("SEQ_INGESTION_API_KEY=");
    }

    [Fact]
    public void DeploymentDoc_DocumentsSeqAuthAndLocalhostBinding()
    {
        // Arrange
        var doc = ReadRepoFile(Path.Combine("docs", "deployment.md"));

        // Assert — section 8 covers the auth variables, the localhost
        // binding, and the first-boot bootstrap (admin login + API key).
        doc.Should().Contain("SEQ_ADMIN_PASSWORD");
        doc.Should().Contain("SEQ_INGESTION_API_KEY");
        doc.Should().Contain("127.0.0.1");
        doc.Should().Contain("API Keys");
        doc.Should().Contain("SEQ_FIRSTRUN_*");
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
            0, $"compose file must define a '{serviceName}:' service");

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
