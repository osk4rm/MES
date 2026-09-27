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

    private static readonly Regex PermissionPattern =
        new(@"`([a-z]+\.(?:write|read))`", RegexOptions.Compiled);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "AsistOff.MES.Production.Api")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate repository root from " + AppContext.BaseDirectory);
    }

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

    /// <summary>
    /// Every API endpoint and permission code documented in the execution
    /// chapters must exist in the Api controllers / RBAC defaults on this
    /// branch, so the manual cannot drift from current behavior
    /// (issue #345, AC2/AC3 correctness, not just structural presence).
    /// </summary>
    [Fact]
    public void Execution_documented_endpoints_and_permissions_match_code()
    {
        var manual = ManualDirectory();
        var apiCorpus = string.Concat(Directory
            .GetFiles(RepositoryRoot(), "*Controller.cs", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}Controllers{Path.DirectorySeparatorChar}"))
            .Select(File.ReadAllText));
        var rbac = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "AsistOff.MES.Users.Core", "Rbac", "RbacDefaults.cs"));

        (string Chapter, string[] Fragments)[] expectations =
        [
            ("07-production-orders.md",
                ["api/production-orders", "/release", "/complete", "/close", "/history"]),
            ("08-dispatch-board.md",
                ["api/schedule", "dispatch"]),
            ("09-confirmations.md",
                ["api/production-confirmations", "/movements"]),
            ("10-scrap-downtime.md",
                ["api/scrap-events", "api/downtime-events", "api/reason-codes", "/close"]),
            ("11-lots-genealogy.md",
                ["api/lots", "api/lot-genealogy", "by-code", "upstream", "downstream"]),
            ("12-gantt-schedule.md",
                ["api/schedule", "gantt", "gantt/segments"]),
            ("13-shift-handover.md",
                ["api/shift-handovers", "context"]),
            ("14-operator-panel.md",
                ["api/schedule", "operator-queue", "api/andon-signals", "acknowledge", "resolve"]),
        ];

        var problems = new List<string>();

        foreach (var (chapter, fragments) in expectations)
        {
            var content = File.ReadAllText(Path.Combine(manual, chapter));

            foreach (var fragment in fragments)
            {
                if (!content.Contains(fragment, StringComparison.Ordinal))
                    problems.Add($"{chapter} does not document '{fragment}'");
                else if (!apiCorpus.Contains(fragment, StringComparison.Ordinal))
                    problems.Add($"'{fragment}' documented in {chapter} has no match in the Api controllers");
            }

            foreach (Match match in PermissionPattern.Matches(content))
            {
                var permission = match.Groups[1].Value;
                if (!rbac.Contains($"\"{permission}\"", StringComparison.Ordinal))
                    problems.Add($"{chapter} documents unknown permission '{permission}'");
            }
        }

        Assert.True(problems.Count == 0,
            "Manual/code drift: " + string.Join("; ", problems));
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
