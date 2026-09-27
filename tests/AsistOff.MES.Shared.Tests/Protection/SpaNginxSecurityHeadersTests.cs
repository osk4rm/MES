using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Protection;

/// <summary>
/// Static proof for the SPA security headers (issue #340). The Gateway's
/// <c>SecurityHeadersMiddleware</c> only covers <c>/api/*</c>, so the web
/// <c>nginx.conf</c> must emit the same conservative set — plus a Vite-SPA
/// CSP — on every nginx-terminated response (index.html shell, hashed
/// assets, config.js, SPA fallback). Unlike whole-file substring checks,
/// every assertion is scoped to the owning <c>location</c> block, so a
/// passing test proves the headers sit on the right location, carry the
/// <c>always</c> parameter (error responses included), and never leak onto
/// the proxied <c>/api/</c> responses (which keep the backend values with no
/// duplication).
/// </summary>
public sealed class SpaNginxSecurityHeadersTests
{
    private static readonly string[] StaticLocations =
    [
        "location = /config.js {",
        "location / {",
        "location ~*",
    ];

    private static readonly string[] ProxyLocations =
    [
        "location /api/ {",
        "location /health {",
        "location /swagger {",
    ];

    [Theory]
    [InlineData("location = /config.js {")]
    [InlineData("location / {")]
    public void Nginx_SpaShellAndConfigJs_CarryFullSecurityHeaderSet(string locationHeader)
    {
        // Arrange — the SPA shell (location / doubles as the SPA fallback
        // via try_files) and the runtime config.js.
        var block = LocationBlock(ReadNginxConf(), locationHeader);

        // Assert — all four headers, always-on so error responses carry them.
        AssertStaticHeaders(block);
    }

    [Fact]
    public void Nginx_StaticAssets_CarryFullSecurityHeaderSet()
    {
        // Arrange — content-hashed bundle assets matched by extension.
        var block = LocationBlock(ReadNginxConf(), "location ~*");

        // Assert
        AssertStaticHeaders(block);
    }

    [Fact]
    public void Nginx_StaticLocations_ShareIdenticalCspValue()
    {
        // Arrange
        var conf = ReadNginxConf();

        // Act
        var policies = StaticLocations
            .Select(l => CspValue(LocationBlock(conf, l)))
            .ToList();

        // Assert — the conf comment requires the three blocks to stay in
        // sync; a drift would serve different policies per resource type.
        policies.Should().HaveCount(3);
        policies[1].Should().Be(policies[0]);
        policies[2].Should().Be(policies[0]);
    }

    [Fact]
    public void Nginx_Csp_RestrictsEveryDirectiveToSelf()
    {
        // Arrange
        var csp = CspValue(LocationBlock(ReadNginxConf(), "location / {"));

        // Assert — the issue-prescribed Vite-SPA baseline: everything
        // same-origin, no framing objects, data: images/fonts only.
        csp.Should().Contain("default-src 'self'");
        csp.Should().Contain("base-uri 'self'");
        csp.Should().Contain("object-src 'none'");
        csp.Should().Contain("frame-ancestors 'self'");
        csp.Should().Contain("form-action 'self'");
        csp.Should().Contain("script-src 'self'");
        csp.Should().Contain("style-src 'self'");
        csp.Should().Contain("img-src 'self' data:");
        csp.Should().Contain("font-src 'self' data:");
        csp.Should().Contain("connect-src 'self'");
    }

    [Fact]
    public void Nginx_Csp_AllowsInlineStylesOnly_WithNoInlineScriptsOrEval()
    {
        // Arrange — style-src 'unsafe-inline' is the single documented
        // widening (Vue 3 renders :style bindings and transitions as inline
        // style attributes); scripts stay external-only, no eval anywhere.
        var csp = CspValue(LocationBlock(ReadNginxConf(), "location / {"));

        // Act
        var scriptSrc = csp.Split(';').Single(d => d.TrimStart().StartsWith("script-src", StringComparison.Ordinal));

        // Assert
        csp.Should().Contain("style-src 'self' 'unsafe-inline'");
        scriptSrc.Should().NotContain("unsafe-inline");
        csp.Should().NotContain("unsafe-eval");
    }

    [Theory]
    [InlineData("location /api/ {")]
    [InlineData("location /health {")]
    [InlineData("location /swagger {")]
    public void Nginx_ProxyLocations_AddNoHeaders_BackendValuesWin(string locationHeader)
    {
        // Arrange — nginx would otherwise append these next to the backend
        // values and clients would see duplicated headers.
        var block = LocationBlock(ReadNginxConf(), locationHeader);

        // Assert
        block.Should().NotContain("add_header");
    }

    [Fact]
    public void Nginx_EmitsNoHsts_TlsTerminatesAtProxy()
    {
        // Arrange — HSTS over the plain-HTTP hop to the Coolify/Traefik
        // proxy would be ignored by browsers anyway; the Gateway still emits
        // it for https /api responses.
        var conf = ReadNginxConf();

        // Assert
        conf.Should().NotContain("Strict-Transport-Security");
    }

    private static void AssertStaticHeaders(string block)
    {
        block.Should().Contain("add_header X-Content-Type-Options \"nosniff\" always;");
        block.Should().Contain("add_header Referrer-Policy \"no-referrer\" always;");
        block.Should().Contain("add_header X-Frame-Options \"DENY\" always;");
        block.Should().Contain("add_header Content-Security-Policy \"");
        block.Should().Contain("\" always;");
    }

    private static string CspValue(string locationBlock)
    {
        var line = locationBlock
            .Split('\n')
            .Single(l => l.Contains("add_header Content-Security-Policy", StringComparison.Ordinal));
        var firstQuote = line.IndexOf('"');
        var lastQuote = line.LastIndexOf('"');
        firstQuote.Should().BeGreaterThanOrEqualTo(0);
        lastQuote.Should().BeGreaterThan(firstQuote);
        return line.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
    }

    /// <summary>
    /// Extracts a single nginx <c>location</c> block by its header line.
    /// <paramref name="locationHeader"/> matches the trimmed opening line
    /// exactly, except a <c>location ~*</c> prefix match for the regex block.
    /// </summary>
    private static string LocationBlock(string conf, string locationHeader)
    {
        var lines = conf.Split('\n');
        var start = Array.FindIndex(lines, l => MatchesLocation(l.Trim(), locationHeader));
        start.Should().BeGreaterThanOrEqualTo(
            0, $"nginx.conf must define '{locationHeader}'");

        var taken = new List<string> { lines[start] };
        for (var i = start + 1; i < lines.Length; i++)
        {
            taken.Add(lines[i]);
            if (lines[i].Trim() == "}")
            {
                break;
            }
        }

        taken.Count.Should().BeGreaterThan(1, $"nginx.conf block '{locationHeader}' must be closed");
        return string.Join('\n', taken);
    }

    private static bool MatchesLocation(string trimmedLine, string locationHeader)
        => locationHeader == "location ~*"
            ? trimmedLine.StartsWith("location ~*", StringComparison.Ordinal)
            : trimmedLine == locationHeader;

    private static string ReadNginxConf()
    {
        var relativePath = Path.Combine("AsistOff.MES.Web", "nginx.conf");
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
