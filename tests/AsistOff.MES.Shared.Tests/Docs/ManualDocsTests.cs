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

    /// <summary>
    /// Slice 2/3 (issue #345) must ship exactly these eight execution
    /// chapters and the README index must link every one of them, so a
    /// dropped chapter fails the build instead of going unnoticed.
    /// </summary>
    public static readonly string[] ExecutionChapters =
    [
        "07-production-orders.md",
        "08-dispatch-board.md",
        "09-confirmations.md",
        "10-scrap-downtime.md",
        "11-lots-genealogy.md",
        "12-gantt-schedule.md",
        "13-shift-handover.md",
        "14-operator-panel.md",
    ];

    [Fact]
    public void Execution_chapters_exist_and_are_linked_from_readme()
    {
        var manual = ManualDirectory();
        var readme = File.ReadAllText(Path.Combine(manual, "README.md"));

        var missingFiles = ExecutionChapters
            .Where(page => !File.Exists(Path.Combine(manual, page)))
            .ToList();
        var unlinked = ExecutionChapters
            .Where(page => !readme.Contains(page, StringComparison.Ordinal))
            .ToList();

        Assert.True(missingFiles.Count == 0,
            "Missing execution chapters: " + string.Join(", ", missingFiles));
        Assert.True(unlinked.Count == 0,
            "Execution chapters not linked from README.md: " + string.Join(", ", unlinked));
    }

    /// <summary>
    /// Every execution chapter must state who may call it (signed-in
    /// read vs. write permission) and must carry an error table anchored
    /// on the shared 401 contract, so permission/validation/error
    /// coverage cannot silently rot (issue #345, AC3).
    /// </summary>
    [Fact]
    public void Execution_chapters_document_access_and_error_cases()
    {
        var manual = ManualDirectory();
        var incomplete = new List<string>();

        foreach (var page in ExecutionChapters)
        {
            var path = Path.Combine(manual, page);
            if (!File.Exists(path))
            {
                incomplete.Add($"{page}: file missing");
                continue;
            }

            var content = File.ReadAllText(path);
            var hasAccess = content.Contains("signed-in", StringComparison.OrdinalIgnoreCase)
                || content.Contains("signed in", StringComparison.OrdinalIgnoreCase);
            var hasErrors = content.Contains("401", StringComparison.Ordinal)
                && content.Contains("Error cases", StringComparison.Ordinal);

            if (!hasAccess || !hasErrors)
                incomplete.Add($"{page}: access={hasAccess}, errors={hasErrors}");
        }

        Assert.True(incomplete.Count == 0,
            "Chapters missing access or error documentation: " + string.Join("; ", incomplete));
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
