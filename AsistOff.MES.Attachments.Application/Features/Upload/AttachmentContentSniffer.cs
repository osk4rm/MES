using AsistOff.MES.Shared.Abstractions.Exceptions;

namespace AsistOff.MES.Attachments.Application.Features.Upload;

/// <summary>
/// Kinds the magic-byte sniffer can distinguish. Only allowlisted kinds exist;
/// anything executable or markup-like is reported as <see cref="Binary"/> or
/// <see cref="Text"/> and then rejected on mismatch with the declared type.
/// </summary>
public enum SniffedContentKind
{
    Unknown,
    Png,
    Jpeg,
    Gif,
    WebP,
    Bmp,
    Pdf,
    Text,
    Binary
}

/// <summary>
/// Verifies that uploaded bytes agree with the declared content type by
/// inspecting magic bytes. Rejects e.g. an HTML payload declared as
/// <c>image/png</c> before anything is persisted.
/// </summary>
public static class AttachmentContentSniffer
{
    /// <summary>Maximum bytes inspected; enough for all supported magic numbers.</summary>
    public const int MaxProbeBytes = 64;

    public static SniffedContentKind DetectKind(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            return SniffedContentKind.Unknown;

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
            return SniffedContentKind.Png;

        // JPEG: FF D8 FF
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return SniffedContentKind.Jpeg;

        // GIF: "GIF87a" / "GIF89a"
        if (bytes.Length >= 6 &&
            bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 &&
            bytes[3] == 0x38 && (bytes[4] == 0x37 || bytes[4] == 0x39) && bytes[5] == 0x61)
            return SniffedContentKind.Gif;

        // WebP: "RIFF" .... "WEBP"
        if (bytes.Length >= 12 &&
            bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            return SniffedContentKind.WebP;

        // BMP: "BM"
        if (bytes.Length >= 2 && bytes[0] == 0x42 && bytes[1] == 0x4D)
            return SniffedContentKind.Bmp;

        // PDF: "%PDF"
        if (bytes.Length >= 4 &&
            bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46)
            return SniffedContentKind.Pdf;

        return IsTextLike(bytes) ? SniffedContentKind.Text : SniffedContentKind.Binary;
    }

    /// <summary>
    /// Throws <see cref="ValidationException"/> when the sniffed bytes disagree
    /// with the declared (already normalized) content type.
    /// </summary>
    public static void EnsureMatches(string normalizedContentType, ReadOnlySpan<byte> bytes)
    {
        var kind = DetectKind(bytes);

        var expected = kind switch
        {
            SniffedContentKind.Png => "image/png",
            SniffedContentKind.Jpeg => "image/jpeg",
            SniffedContentKind.Gif => "image/gif",
            SniffedContentKind.WebP => "image/webp",
            SniffedContentKind.Bmp => "image/bmp",
            SniffedContentKind.Pdf => "application/pdf",
            SniffedContentKind.Text => "text",
            SniffedContentKind.Binary => "binary",
            _ => null
        };

        // Tiny or ambiguous payloads: only reject obvious binary-as-text lies.
        if (expected is null)
            return;

        var matches = expected switch
        {
            "text" => normalizedContentType.StartsWith("text/", StringComparison.Ordinal),
            "binary" => normalizedContentType is "application/octet-stream",
            _ => string.Equals(normalizedContentType, expected, StringComparison.Ordinal)
        };

        if (!matches)
            throw new ValidationException("file", $"Uploaded content does not match the declared content type '{normalizedContentType}'.");
    }

    /// <summary>
    /// Text-like means: no NUL bytes and every byte is printable ASCII,
    /// whitespace, or part of a plausible UTF-8 sequence. HTML/JS/EXE payloads
    /// either contain NUL bytes or are text whose kind still mismatches an
    /// image/pdf declaration.
    /// </summary>
    private static bool IsTextLike(ReadOnlySpan<byte> bytes)
    {
        var probe = bytes[..Math.Min(bytes.Length, 4096)];

        foreach (var b in probe)
        {
            if (b == 0x00)
                return false;

            if (b is 0x09 or 0x0A or 0x0D || (b >= 0x20 && b <= 0x7E))
                continue;

            // Allow non-ASCII bytes (UTF-8 text); only NUL disqualifies here.
            if (b < 0x20)
                return false;
        }

        return true;
    }
}
