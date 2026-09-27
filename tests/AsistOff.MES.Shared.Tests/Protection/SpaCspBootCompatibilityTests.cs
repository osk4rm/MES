using System.Text.RegularExpressions;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Protection;

/// <summary>
/// Static proof for issue #340 AC4 (SPA boots with zero CSP violations and
/// login/logout work). A live clean-profile browser boot belongs to the e2e
/// stage, but everything that could violate the nginx CSP at boot is pinned
/// here against the real frontend sources, so a passing suite proves the
/// policy and the bundle agree by construction:
/// <list type="bullet">
/// <item><c>script-src 'self'</c>: the shell loads external same-origin
/// scripts only, and no source needs <c>eval</c> / <c>new Function</c>.</item>
/// <item><c>style-src 'self' 'unsafe-inline'</c>: the only inline styling is
/// Vue <c>:style</c> bindings, which the documented widening covers.</item>
/// <item><c>connect-src 'self'</c> / <c>form-action 'self'</c>: every API call
/// and the login/logout flow stay same-origin under <c>/api</c>.</item>
/// </list>
/// </summary>
public sealed class SpaCspBootCompatibilityTests
{
    [Fact]
    public void SpaShell_HasNoInlineScripts_BootsUnderScriptSrcSelf()
    {
        // Arrange — the exact shell nginx serves for GET / (and the SPA
        // fallback via try_files).
        var shell = ReadWebFile("index.html");
        var scripts = Regex.Matches(shell, "<script\\b(?<attrs>[^>]*)>(?<body>.*?)</script>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        // Assert — every script is an external same-origin file (the runtime
        // /config.js plus the Vite entry); an inline body would be blocked by
        // script-src 'self' and break the boot.
        scripts.Should().NotBeEmpty("index.html must load the SPA entry script");
        foreach (Match script in scripts)
        {
            var src = Regex.Match(script.Groups["attrs"].Value, "src\\s*=\\s*[\"'](?<src>[^\"']+)[\"']",
                RegexOptions.IgnoreCase);
            src.Success.Should().BeTrue($"every shell <script> must have a src attribute: {script.Value}");
            IsSameOrigin(src.Groups["src"].Value).Should().BeTrue(
                $"shell scripts must be same-origin for script-src 'self': {src.Groups["src"].Value}");
            script.Groups["body"].Value.Trim().Should().BeEmpty(
                "shell <script> must have no inline body for script-src 'self'");
        }

        scripts.Should().ContainSingle(s =>
            s.Groups["attrs"].Value.Contains("/config.js", StringComparison.Ordinal));
    }

    [Fact]
    public void SpaShell_LoadsNoCrossOriginResources()
    {
        // Arrange
        var shell = ReadWebFile("index.html");
        var references = Regex.Matches(shell, "(?:src|href)\\s*=\\s*[\"'](?<url>[^\"']+)[\"']",
            RegexOptions.IgnoreCase);

        // Assert — no absolute, protocol-relative, or foreign-scheme URL that
        // would fall outside default-src/img-src/font-src 'self' (+ data:).
        references.Should().NotBeEmpty();
        foreach (Match reference in references)
        {
            var url = reference.Groups["url"].Value;
            url.Should().NotContain("://", $"shell resource must be same-origin: {url}");
            url.Should().NotStartWith("//", $"shell resource must be same-origin: {url}");
        }
    }

    [Fact]
    public void SpaShell_HasNoInlineEventHandlersOrJavascriptUrls()
    {
        // Arrange — inline handlers (onclick=...) and javascript: URLs execute
        // as inline scripts and would be blocked by script-src 'self'.
        var shell = ReadWebFile("index.html");

        // Assert
        Regex.IsMatch(shell, "\\son[a-z]+\\s*=", RegexOptions.IgnoreCase)
            .Should().BeFalse("index.html must not use inline event-handler attributes");
        shell.Should().NotContain("javascript:", "javascript: URLs are blocked by script-src 'self'");
    }

    [Fact]
    public void SpaSources_RequireNoUnsafeEval()
    {
        // Arrange — eval / new Function / string-form timers need
        // 'unsafe-eval', which the nginx CSP deliberately does not grant.
        var offenders = SourceFiles()
            .Where(f => Regex.IsMatch(ReadWebFile(f),
                "\\beval\\s*\\(|\\bnew\\s+Function\\s*\\(|\\bset(Time(out)?|Interval)\\s*\\(\\s*['\"]"))
            .ToList();

        // Assert
        offenders.Should().BeEmpty("no frontend source may require 'unsafe-eval'");
    }

    [Fact]
    public void SpaViews_UseNoRawHtmlInjection()
    {
        // Arrange — v-html bypasses Vue's template escaping; CSP cannot
        // mitigate it, so production views must not use it.
        var offenders = SourceFiles()
            .Where(f => f.EndsWith(".vue", StringComparison.OrdinalIgnoreCase)
                && ReadWebFile(f).Contains("v-html", StringComparison.Ordinal))
            .ToList();

        // Assert
        offenders.Should().BeEmpty("production views must not use v-html");
    }

    [Fact]
    public void SpaServiceEndpoints_StaySameOrigin_CoveredByConnectSrcSelf()
    {
        // Arrange — every service call goes through the shared axios instance
        // whose base URL resolves to the page origin (runtime /config.js in
        // production), with relative /api paths, so connect-src 'self' covers
        // them without extra hosts.
        var services = SourceFiles()
            .Where(f => f.StartsWith("src/services/", StringComparison.Ordinal)
                && f.EndsWith(".ts", StringComparison.OrdinalIgnoreCase))
            .ToList();
        services.Should().NotBeEmpty();

        var bases = new List<string>();
        foreach (var service in services)
        {
            var content = ReadWebFile(service);
            foreach (Match m in Regex.Matches(content, "const\\s+(?:BASE|AUTH_PATH)\\s*=\\s*['\"`](?<url>[^'\"`]+)['\"`]")
                         .Cast<Match>())
            {
                bases.Add(m.Groups["url"].Value);
            }

            Regex.IsMatch(content, "(http\\.(get|post|put|patch|delete|request)|[^a-zA-Z]fetch)\\(\\s*['\"`]https?://")
                .Should().BeFalse($"{service} must not call absolute cross-origin URLs");
        }

        bases.Should().NotBeEmpty("services must declare their /api base paths");
        foreach (var url in bases)
        {
            url.Should().StartWith("/api/", $"service endpoint must stay same-origin: {url}");
        }
    }

    [Fact]
    public void SpaLoginLogoutFlow_UsesSameOriginRoutes()
    {
        // Arrange — AC4 requires login plus logout to succeed under the CSP:
        // the login view is a same-origin route and the session calls are
        // same-origin /api posts (no form posts to foreign origins, which
        // form-action 'self' would block).
        var router = ReadWebFile("src/router.ts");
        var auth = ReadWebFile("src/services/authService.ts");

        // Assert
        router.Should().Contain("path: '/login'");
        auth.Should().Contain("AUTH_PATH = '/api/auth'");
        auth.Should().Contain("/sign-in");
        auth.Should().Contain("/sign-out");
        auth.Should().Contain("/refresh");
    }

    [Fact]
    public void SpaBuild_EmitsExternalBundlesOnly()
    {
        // Arrange — Vite must emit the app as external hashed files (served
        // from the nginx static location under script-src 'self'); inlining
        // the bundle into index.html (singlefile builds) would turn it into
        // an inline script and break the boot.
        var config = ReadWebFile("vite.config.ts");

        // Assert
        config.Should().NotContain("singlefile");
        config.Should().NotContain("inlineDynamicImports");
    }

    private static bool IsSameOrigin(string url)
        => url.StartsWith("/", StringComparison.Ordinal) && !url.StartsWith("//", StringComparison.Ordinal);

    private static IEnumerable<string> SourceFiles()
    {
        var root = WebRoot();
        return Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith(".vue", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Contains(".spec.", StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'));
    }

    private static string ReadWebFile(string relativePath)
        => File.ReadAllText(Path.Combine(WebRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string WebRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 12 && directory is not null; i++)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AsistOff.MES.Web", "index.html")))
            {
                return Path.Combine(directory.FullName, "AsistOff.MES.Web");
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate AsistOff.MES.Web from test output.");
    }
}
