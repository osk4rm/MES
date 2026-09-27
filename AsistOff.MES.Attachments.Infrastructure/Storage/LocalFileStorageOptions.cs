namespace AsistOff.MES.Attachments.Infrastructure.Storage;

public class LocalFileStorageOptions
{
    public const string SectionName = "Attachments:LocalStorage";

    /// <summary>Absolute or content-root-relative root directory for blobs.</summary>
    public string RootPath { get; set; } = "App_Data/attachments";

    /// <summary>
    /// Resolves the configured root to an absolute path. Relative paths are
    /// resolved against the host content root (issue #373) so the blob root
    /// does not depend on the process working directory; absolute paths pass
    /// through unchanged. A blank value falls back to the default.
    /// </summary>
    public static string ResolveRootPath(string? rootPath, string contentRootPath)
    {
        var configured = string.IsNullOrWhiteSpace(rootPath) ? "App_Data/attachments" : rootPath;
        if (Path.IsPathRooted(configured))
            return Path.GetFullPath(configured);

        return Path.GetFullPath(Path.Combine(contentRootPath, configured));
    }
}
