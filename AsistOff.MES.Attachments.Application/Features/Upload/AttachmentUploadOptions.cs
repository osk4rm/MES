namespace AsistOff.MES.Attachments.Application.Features.Upload;

/// <summary>
/// Configurable allowlist for attachment uploads. When the
/// <c>Attachments:Upload</c> section is absent, the safe defaults below apply:
/// raster images, PDF, plain text and CSV.
/// </summary>
public sealed class AttachmentUploadOptions
{
    public const string SectionName = "Attachments:Upload";

    /// <summary>Default maximum upload size: 10 MiB. Enforced before persistence.</summary>
    public const long DefaultMaxFileSizeBytes = 10 * 1024 * 1024;

    public long MaxFileSizeBytes { get; set; } = DefaultMaxFileSizeBytes;

    public HashSet<string> AllowedExtensions { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp",
        ".pdf",
        ".txt", ".csv"
    };

    public HashSet<string> AllowedContentTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/gif", "image/webp", "image/bmp",
        "application/pdf",
        "text/plain", "text/csv"
    };

    /// <summary>
    /// Normalizes operator-supplied configuration so allowlist semantics stay
    /// identical to the safe defaults: case-insensitive matching, trimmed
    /// entries, MIME parameters stripped, extensions dotted, and a positive
    /// max size. Configuration binding replaces the <see cref="HashSet{T}"/>
    /// instances (losing the <see cref="StringComparer.OrdinalIgnoreCase"/>
    /// comparer) and never normalizes values, so without this step a custom
    /// <c>Attachments:Upload</c> section would silently change match behavior.
    /// An explicitly emptied list is respected (deny-all, fail-closed); a
    /// <c>null</c> list (config error) falls back to the safe defaults.
    /// </summary>
    public void Normalize()
    {
        if (MaxFileSizeBytes <= 0)
            MaxFileSizeBytes = DefaultMaxFileSizeBytes;

        AllowedExtensions = NormalizeExtensions(AllowedExtensions) ?? new HashSet<string>(DefaultExtensions(), StringComparer.OrdinalIgnoreCase);
        AllowedContentTypes = NormalizeContentTypes(AllowedContentTypes) ?? new HashSet<string>(DefaultContentTypes(), StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string>? NormalizeExtensions(HashSet<string>? values)
    {
        if (values is null)
            return null;

        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in values)
        {
            var extension = raw?.Trim();
            if (string.IsNullOrEmpty(extension))
                continue;

            if (!extension.StartsWith('.'))
                extension = "." + extension;

            normalized.Add(extension);
        }

        return normalized;
    }

    private static HashSet<string>? NormalizeContentTypes(HashSet<string>? values)
    {
        if (values is null)
            return null;

        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in values)
        {
            var mime = AttachmentUploadGuard.NormalizeContentType(raw);
            if (string.IsNullOrEmpty(mime))
                continue;

            normalized.Add(mime);
        }

        return normalized;
    }

    private static IEnumerable<string> DefaultExtensions() =>
    [
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp",
        ".pdf",
        ".txt", ".csv"
    ];

    private static IEnumerable<string> DefaultContentTypes() =>
    [
        "image/png", "image/jpeg", "image/gif", "image/webp", "image/bmp",
        "application/pdf",
        "text/plain", "text/csv"
    ];
}
