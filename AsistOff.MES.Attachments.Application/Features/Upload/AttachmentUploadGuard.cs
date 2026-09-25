using AsistOff.MES.Shared.Abstractions.Exceptions;

namespace AsistOff.MES.Attachments.Application.Features.Upload;

/// <summary>
/// Server-side upload allowlist guard. Both the file extension and the declared
/// MIME type must be allowlisted; the caller-supplied content type is normalized
/// (lowercased, parameters stripped) and never trusted beyond this check.
/// </summary>
public static class AttachmentUploadGuard
{
    /// <summary>
    /// Normalizes a caller-supplied content type: trims, lowercases and strips
    /// any <c>; charset=...</c> style parameters.
    /// </summary>
    public static string NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return string.Empty;

        var mime = contentType.Split(';', 2)[0].Trim().ToLowerInvariant();

        // Common non-standard alias; treat as canonical image/jpeg.
        return mime == "image/jpg" ? "image/jpeg" : mime;
    }

    /// <summary>
    /// Throws <see cref="ValidationException"/> when the extension or the
    /// declared content type is not allowlisted. Returns the normalized content type.
    /// </summary>
    public static string EnsureAllowed(string fileName, string? contentType, AttachmentUploadOptions options)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty);
        if (string.IsNullOrEmpty(extension) || !options.AllowedExtensions.Contains(extension))
            throw new ValidationException(nameof(fileName), $"File extension '{extension}' is not allowed.");

        var normalized = NormalizeContentType(contentType);
        if (string.IsNullOrEmpty(normalized) || !options.AllowedContentTypes.Contains(normalized))
            throw new ValidationException(nameof(contentType), $"Content type '{contentType}' is not allowed.");

        return normalized;
    }

    /// <summary>
    /// Conservative download mapping: allowlisted types are served as-is,
    /// everything else falls back to <c>application/octet-stream</c> so a
    /// stored attacker-controlled type is never echoed verbatim.
    /// </summary>
    public static string MapDownloadContentType(string? storedContentType, AttachmentUploadOptions options)
    {
        var normalized = NormalizeContentType(storedContentType);
        return options.AllowedContentTypes.Contains(normalized)
            ? normalized
            : "application/octet-stream";
    }
}
