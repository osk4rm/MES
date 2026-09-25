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
}
