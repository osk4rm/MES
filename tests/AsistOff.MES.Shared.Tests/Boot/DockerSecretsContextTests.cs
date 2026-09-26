using System.Text.RegularExpressions;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Boot;

/// <summary>
/// Static canary proof for the Docker build context (issue #308).
/// Docker is unavailable in some sandboxes and unit-test runners, so these
/// tests evaluate the <c>.dockerignore</c> patterns with Docker-compatible
/// matching semantics against the exact canary files from the PR procedure:
/// every canary secret must be excluded while real sources stay included
/// (guarding against over-ignoring that would break the image).
/// The live <c>docker build</c> + <c>docker save</c> proof runs in CI
/// (the <c>docker</c> job in <c>.github/workflows/ci.yml</c>).
/// Companion assertions pin the digest-pinned <c>FROM</c> lines and the
/// non-root <c>USER</c> lines so the ignore hardening cannot silently trade
/// away the guarantees from issue #271.
/// </summary>
public sealed class DockerSecretsContextTests
{
    [Theory]
    [InlineData(".env")]
    [InlineData(".env.local")]
    [InlineData(".env.production")]
    [InlineData("AsistOff.MES.Web/.env.local")]
    [InlineData("AsistOff.MES.Web/.env.production")]
    [InlineData(".env.development")]
    [InlineData("config/.env")]
    public void ApiContext_CanarySecretFiles_AreExcluded(string contextPath)
    {
        // Arrange
        var dockerignore = ReadRepoFile(".dockerignore");

        // Act + Assert
        IsExcluded(dockerignore, contextPath).Should().BeTrue(
            $"canary file '{contextPath}' must never enter the API build context");
    }

    [Theory]
    [InlineData("secrets.json")]
    [InlineData("AsistOff.MES.Gateway/secrets.json")]
    [InlineData("AsistOff.MES.Users.Api/appsettings.secrets.json")]
    public void ApiContext_UserSecrets_AreExcluded(string contextPath)
    {
        // Arrange
        var dockerignore = ReadRepoFile(".dockerignore");

        // Act + Assert
        IsExcluded(dockerignore, contextPath).Should().BeTrue(
            $"user-secrets file '{contextPath}' must never enter the API build context");
    }

    [Theory]
    [InlineData("docker-compose.override.yml")]
    [InlineData("docker-compose.swarm.yml")]
    [InlineData("compose.override.yml")]
    public void ApiContext_ComposeOverrides_AreExcluded(string contextPath)
    {
        // Arrange
        var dockerignore = ReadRepoFile(".dockerignore");

        // Act + Assert
        IsExcluded(dockerignore, contextPath).Should().BeTrue(
            $"compose override '{contextPath}' carries local secret references and must never enter the API build context");
    }

    [Theory]
    [InlineData("Dockerfile")]
    [InlineData("AsistOff.MES.Gateway/AsistOff.MES.Gateway.csproj")]
    [InlineData("docker-compose.yml")]
    public void ApiContext_RealSources_StayIncluded(string contextPath)
    {
        // Arrange — over-ignoring would break the image build, so the base
        // compose file, the Dockerfile itself and the restore csprojs must
        // remain visible to the daemon.
        var dockerignore = ReadRepoFile(".dockerignore");

        // Act + Assert
        IsExcluded(dockerignore, contextPath).Should().BeFalse(
            $"real source '{contextPath}' must stay in the API build context");
    }

    [Theory]
    [InlineData(".env")]
    [InlineData(".env.local")]
    [InlineData(".env.production")]
    [InlineData("config/.env.local")]
    public void WebContext_CanarySecretFiles_AreExcluded(string contextPath)
    {
        // Arrange — Vite loads .env* at build time, so any stray file bakes
        // straight into the bundle layers.
        var dockerignore = ReadRepoFile(Path.Combine("AsistOff.MES.Web", ".dockerignore"));

        // Act + Assert
        IsExcluded(dockerignore, contextPath).Should().BeTrue(
            $"canary file '{contextPath}' must never enter the web build context");
    }

    [Theory]
    [InlineData("secrets.json")]
    [InlineData("config/secrets.json")]
    [InlineData("AsistOff.MES.Users.Api/appsettings.secrets.json")]
    public void WebContext_UserSecrets_AreExcluded(string contextPath)
    {
        // Arrange
        var dockerignore = ReadRepoFile(Path.Combine("AsistOff.MES.Web", ".dockerignore"));

        // Act + Assert
        IsExcluded(dockerignore, contextPath).Should().BeTrue(
            $"user-secrets file '{contextPath}' must never enter the web build context");
    }

    [Theory]
    [InlineData("package.json")]
    [InlineData("Dockerfile")]
    [InlineData("nginx.conf")]
    [InlineData("src/main.ts")]
    public void WebContext_RealSources_StayIncluded(string contextPath)
    {
        // Arrange
        var dockerignore = ReadRepoFile(Path.Combine("AsistOff.MES.Web", ".dockerignore"));

        // Act + Assert
        IsExcluded(dockerignore, contextPath).Should().BeFalse(
            $"real source '{contextPath}' must stay in the web build context");
    }

    [Fact]
    public void ApiDockerfile_DigestPinsAndNonRootUser_AreUnchanged()
    {
        // Arrange
        var dockerfile = ReadRepoFile("Dockerfile");
        var fromLines = dockerfile
            .Split('\n')
            .Where(l => l.TrimStart().StartsWith("FROM ", StringComparison.Ordinal))
            .ToList();

        // Assert — sdk + aspnet stages stay digest-pinned (issue #271).
        fromLines.Should().HaveCountGreaterThanOrEqualTo(
            2, "the API Dockerfile must keep its build + runtime stages");
        fromLines.Should().OnlyContain(
            l => l.Contains("@sha256:", StringComparison.Ordinal),
            "every API FROM line must stay digest-pinned (issue #271)");

        // Assert — non-root runtime user and csproj-first layer caching stay.
        dockerfile.Should().Contain("USER app");
        dockerfile.Should().Contain("COPY AsistOff.MES.Gateway/AsistOff.MES.Gateway.csproj");
        dockerfile.Should().Contain("COPY . .");
    }

    [Fact]
    public void WebDockerfile_DigestPinsAndNonRootUser_AreUnchanged()
    {
        // Arrange
        var dockerfile = ReadRepoFile(Path.Combine("AsistOff.MES.Web", "Dockerfile"));
        var fromLines = dockerfile
            .Split('\n')
            .Where(l => l.TrimStart().StartsWith("FROM ", StringComparison.Ordinal))
            .ToList();

        // Assert — node + nginx stages stay digest-pinned (issue #271).
        fromLines.Should().HaveCountGreaterThanOrEqualTo(
            2, "the web Dockerfile must keep its build + serve stages");
        fromLines.Should().OnlyContain(
            l => l.Contains("@sha256:", StringComparison.Ordinal),
            "every web FROM line must stay digest-pinned (issue #271)");

        // Assert — non-root serve user stays.
        dockerfile.Should().Contain("USER nginx");
        dockerfile.Should().Contain("COPY . .");
    }

    /// <summary>
    /// Last-match-wins exclusion following <c>.dockerignore</c> semantics:
    /// blank lines and <c>#</c> comments are skipped, <c>!</c> negates.
    /// </summary>
    private static bool IsExcluded(string dockerignore, string contextPath)
    {
        var excluded = false;
        foreach (var raw in dockerignore.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var negated = line.StartsWith('!');
            var pattern = negated ? line[1..] : line;
            if (MatchesDockerPattern(pattern, contextPath))
            {
                excluded = !negated;
            }
        }

        return excluded;
    }

    /// <summary>
    /// Docker-compatible glob matching for the pattern subset used by this
    /// repo's ignore files: <c>*</c> never crosses <c>/</c>, <c>**</c>
    /// crosses any number of directories (including zero), and a pattern
    /// without wildcards also excludes everything beneath it (directory).
    /// </summary>
    private static bool MatchesDockerPattern(string pattern, string contextPath)
    {
        pattern = pattern.Replace('\\', '/').TrimStart('/');
        var path = contextPath.Replace('\\', '/').TrimStart('/');

        var body = new System.Text.StringBuilder();
        var hasWildcard = false;
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '*')
            {
                if (i + 1 < pattern.Length && pattern[i + 1] == '*')
                {
                    hasWildcard = true;
                    if (i + 2 < pattern.Length && pattern[i + 2] == '/')
                    {
                        body.Append("(.*/)?");
                        i += 2;
                    }
                    else
                    {
                        body.Append(".*");
                        i++;
                    }
                }
                else
                {
                    hasWildcard = true;
                    body.Append("[^/]*");
                }
            }
            else if (c == '?')
            {
                hasWildcard = true;
                body.Append("[^/]");
            }
            else
            {
                body.Append(Regex.Escape(c.ToString()));
            }
        }

        var regex = hasWildcard
            ? $"^{body}$"
            : $"^{body}(/.*)?$";
        return Regex.IsMatch(path, regex);
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
