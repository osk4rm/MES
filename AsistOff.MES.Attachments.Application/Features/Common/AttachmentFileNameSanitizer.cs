namespace AsistOff.MES.Attachments.Application.Features.Common;

/// <summary>
/// Produces download-safe ASCII filenames: strips directories, CR/LF and
/// header-injection characters, and replaces anything outside a conservative
/// set with <c>_</c> so the value can be echoed in a
/// <c>Content-Disposition: attachment</c> header.
/// </summary>
public static class AttachmentFileNameSanitizer
{
    public const int MaxLength = 150;

    public static string Sanitize(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "file";

        // Strip any directory components (both separators; also handles "..").
        var name = fileName.Replace('\\', '/');
        name = name.Split('/').LastOrDefault() ?? string.Empty;
        name = name.Trim();

        if (string.IsNullOrEmpty(name) || name is "." or "..")
            return "file";

        var builder = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
        {
            builder.Append(IsSafe(c) ? c : '_');
        }

        var cleaned = builder.ToString().Trim('.', ' ');
        if (cleaned.Length > MaxLength)
            cleaned = cleaned[..MaxLength].TrimEnd('.', ' ');

        return string.IsNullOrEmpty(cleaned) ? "file" : cleaned;
    }

    private static bool IsSafe(char c) =>
        (c >= 'a' && c <= 'z') ||
        (c >= 'A' && c <= 'Z') ||
        (c >= '0' && c <= '9') ||
        c is '.' or '-' or '_';
}
