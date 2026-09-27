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

    /// <summary>
    /// Default multipart overhead margin above <see cref="DefaultMaxFileSizeBytes"/>:
    /// 1 MiB. The edge <c>RequestSizeLimit</c> is app cap plus this margin so
    /// oversized payloads are rejected by Kestrel before buffering.
    /// </summary>
    public const long DefaultEdgeMarginBytes = 1 * 1024 * 1024;

    /// <summary>
    /// Edge request-size limit compiled into
    /// <c>AttachmentsController.UploadAsync</c>: app cap plus multipart margin
    /// (11 MiB by default). Must stay a <c>const</c> for the attribute; when an
    /// operator raises <see cref="MaxFileSizeBytes"/> via configuration, raise
    /// the edge accordingly (see <see cref="EdgeLimitFor"/>).
    /// </summary>
    public const long EdgeRequestSizeLimitBytes = DefaultMaxFileSizeBytes + DefaultEdgeMarginBytes;

    /// <summary>
    /// Default per-tenant attachment storage quota: 500 MiB of summed
    /// <c>SizeBytes</c> across the tenant's attachments.
    /// </summary>
    public const long DefaultMaxTotalBytesPerTenant = 500L * 1024 * 1024;

    public long MaxFileSizeBytes { get; set; } = DefaultMaxFileSizeBytes;

    /// <summary>
    /// Multipart overhead margin added to <see cref="MaxFileSizeBytes"/> to form
    /// the effective edge limit (see <see cref="EdgeLimitFor"/>).
    /// </summary>
    public long EdgeMarginBytes { get; set; } = DefaultEdgeMarginBytes;

    /// <summary>
    /// Per-tenant storage quota in bytes. Uploads that would push the tenant's
    /// summed attachment sizes above this value are rejected (HTTP 409).
    /// Deleting an attachment frees quota.
    /// </summary>
    public long MaxTotalBytesPerTenant { get; set; } = DefaultMaxTotalBytesPerTenant;

    /// <summary>
    /// Optional malware-scanner endpoint (e.g. a ClamAV sidecar HTTP proxy URL).
    /// Empty means no external scanner is configured: uploads are allowed with
    /// a logged warning (plus the built-in EICAR probe). When set, the scanner
    /// POSTs the file bytes there; an unreachable endpoint fails closed
    /// (upload rejected). Example: <c>http://clamav-proxy:8080/scan</c>.
    /// </summary>
    public string? MalwareScannerEndpoint { get; set; }

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

        if (EdgeMarginBytes <= 0)
            EdgeMarginBytes = DefaultEdgeMarginBytes;

        if (MaxTotalBytesPerTenant <= 0)
            MaxTotalBytesPerTenant = DefaultMaxTotalBytesPerTenant;

        if (string.IsNullOrWhiteSpace(MalwareScannerEndpoint))
            MalwareScannerEndpoint = null;
        else
            MalwareScannerEndpoint = MalwareScannerEndpoint.Trim();

        AllowedExtensions = NormalizeExtensions(AllowedExtensions) ?? new HashSet<string>(DefaultExtensions(), StringComparer.OrdinalIgnoreCase);
        AllowedContentTypes = NormalizeContentTypes(AllowedContentTypes) ?? new HashSet<string>(DefaultContentTypes(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Effective edge limit for the given options: app cap plus multipart margin.
    /// Keep <see cref="EdgeRequestSizeLimitBytes"/> (= the default evaluation of
    /// this method) in sync with the <c>RequestSizeLimit</c> on the controller.
    /// </summary>
    public static long EdgeLimitFor(AttachmentUploadOptions options)
        => options.MaxFileSizeBytes + options.EdgeMarginBytes;

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
