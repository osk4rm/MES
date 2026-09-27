using System.Text.RegularExpressions;

namespace AsistOff.MES.Shared.Tests.Docs;

/// <summary>
/// Guards the end-user manual (docs/manual/): every relative Markdown
/// link must resolve to an existing file and no chapter may carry
/// TODO/TBD/placeholder markers (issue #345 acceptance criteria).
/// </summary>
public sealed class ManualDocsTests
{
    private static readonly Regex LinkPattern =
        new(@"\[[^\]]*\]\(([^)]+)\)", RegexOptions.Compiled);

    private static readonly Regex MarkerPattern =
        new(@"\bTODO\b|\bTBD\b|\bFIXME\b|\bLOREM\b|PLACEHOLDER",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static string ManualDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "manual");
            if (Directory.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate docs/manual from " + AppContext.BaseDirectory);
    }

    [Fact]
    public void Readme_links_resolve_to_existing_pages()
    {
        var manual = ManualDirectory();
        var readme = Path.Combine(manual, "README.md");
        Assert.True(File.Exists(readme), "docs/manual/README.md must exist.");

        var broken = ResolveRelativeLinks(readme, manual)
            .Where(target => !File.Exists(target))
            .ToList();

        Assert.True(broken.Count == 0,
            "Broken relative links in README.md: " + string.Join(", ", broken));
    }

    [Fact]
    public void All_pages_have_zero_broken_relative_links()
    {
        var manual = ManualDirectory();
        var broken = new List<string>();
        var pageCount = 0;

        foreach (var page in Directory.GetFiles(manual, "*.md"))
        {
            pageCount++;
            foreach (var target in ResolveRelativeLinks(page, manual))
            {
                if (!File.Exists(target))
                    broken.Add($"{Path.GetFileName(page)} -> {target}");
            }
        }

        Assert.True(pageCount > 0, "Expected at least one manual page.");
        Assert.True(broken.Count == 0,
            "Broken relative links: " + string.Join("; ", broken));
    }

    [Fact]
    public void No_page_contains_todo_or_placeholder_markers()
    {
        var manual = ManualDirectory();
        var flagged = new List<string>();

        foreach (var page in Directory.GetFiles(manual, "*.md"))
        {
            var content = File.ReadAllText(page);
            var match = MarkerPattern.Match(content);
            if (match.Success)
                flagged.Add($"{Path.GetFileName(page)}: '{match.Value}'");
        }

        Assert.True(flagged.Count == 0,
            "Marker text remains: " + string.Join("; ", flagged));
    }

    private static IEnumerable<string> ResolveRelativeLinks(string pagePath, string manual)
    {
        var content = File.ReadAllText(pagePath);
        var pageDirectory = Path.GetDirectoryName(pagePath) ?? manual;

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
