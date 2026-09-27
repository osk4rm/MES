using System.Text.RegularExpressions;

namespace AsistOff.MES.Shared.Tests.Docs;

/// <summary>
/// Guards the technical specification (docs/spec/): every relative Markdown
/// link must resolve to an existing file and no page may carry
/// TODO/TBD/placeholder markers (issue #352 acceptance criteria).
/// </summary>
public sealed class SpecDocsTests
{
    private static readonly Regex LinkPattern =
        new(@"\[[^\]]*\]\(([^)]+)\)", RegexOptions.Compiled);

    private static readonly Regex MarkerPattern =
        new(@"\bTODO\b|\bTBD\b|\bFIXME\b|\bLOREM\b|PLACEHOLDER",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static string SpecDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "spec");
            if (Directory.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate docs/spec from " + AppContext.BaseDirectory);
    }

    /// <summary>
    /// Slice 2/2 (issue #352) must ship exactly these four pages and the
    /// README index must link every one of them, so a dropped page fails
    /// the build instead of going unnoticed.
    /// </summary>
    public static readonly string[] Slice2Pages =
    [
        "endpoints.md",
        "deployment.md",
        "observability.md",
        "frontend.md",
    ];

    [Fact]
    public void Slice2_pages_exist_and_are_linked_from_readme()
    {
        var spec = SpecDirectory();
        var readme = File.ReadAllText(Path.Combine(spec, "README.md"));

        var missingFiles = Slice2Pages
            .Where(page => !File.Exists(Path.Combine(spec, page)))
            .ToList();
        var unlinked = Slice2Pages
            .Where(page => !readme.Contains(page, StringComparison.Ordinal))
            .ToList();

        Assert.True(missingFiles.Count == 0,
            "Missing spec pages: " + string.Join(", ", missingFiles));
        Assert.True(unlinked.Count == 0,
            "Spec pages not linked from README.md: " + string.Join(", ", unlinked));
    }

    [Fact]
    public void Readme_links_resolve_to_existing_pages()
    {
        var spec = SpecDirectory();
        var readme = Path.Combine(spec, "README.md");
        Assert.True(File.Exists(readme), "docs/spec/README.md must exist.");

        var broken = ResolveRelativeLinks(readme, spec)
            .Where(target => !File.Exists(target))
            .ToList();

        Assert.True(broken.Count == 0,
            "Broken relative links in README.md: " + string.Join(", ", broken));
    }

    [Fact]
    public void All_pages_have_zero_broken_relative_links()
    {
        var spec = SpecDirectory();
        var broken = new List<string>();
        var pageCount = 0;

        foreach (var page in Directory.GetFiles(spec, "*.md"))
        {
            pageCount++;
            foreach (var target in ResolveRelativeLinks(page, spec))
            {
                if (!File.Exists(target))
                    broken.Add($"{Path.GetFileName(page)} -> {target}");
            }
        }

        Assert.True(pageCount > 0, "Expected at least one spec page.");
        Assert.True(broken.Count == 0,
            "Broken relative links: " + string.Join("; ", broken));
    }

    [Fact]
    public void No_page_contains_todo_or_placeholder_markers()
    {
        var spec = SpecDirectory();
        var flagged = new List<string>();

        foreach (var page in Directory.GetFiles(spec, "*.md"))
        {
            var content = File.ReadAllText(page);
            var match = MarkerPattern.Match(content);
            if (match.Success)
                flagged.Add($"{Path.GetFileName(page)}: '{match.Value}'");
        }

        Assert.True(flagged.Count == 0,
            "Marker text remains: " + string.Join("; ", flagged));
    }

    private static IEnumerable<string> ResolveRelativeLinks(string pagePath, string spec)
    {
        var content = File.ReadAllText(pagePath);
        var pageDirectory = Path.GetDirectoryName(pagePath) ?? spec;

        foreach (Match match in LinkPattern.Matches(content))
        {
            var target = match.Groups[1].Value.Trim();
            if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                || target.StartsWith('#')
                || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                continue;

            var path = target.Split('#')[0];
            if (string.IsNullOrWhiteSpace(path))
                continue;

            yield return Path.GetFullPath(Path.Combine(pageDirectory, path));
        }
    }
}
