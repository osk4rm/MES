using AsistOff.MES.Shared.Abstractions.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AsistOff.MES.Attachments.Infrastructure.Storage;

/// <summary>
/// Stores files on the local filesystem under a configured root. Blobs are
/// sharded into two-level directories based on the generated id to avoid very
/// large flat folders. Returned storage key is the POSIX relative path under
/// the root (e.g. <c>ab/cd/abcd...-originalName.pdf</c>).
/// A relative root is resolved against the host content root (issue #373) so
/// the blob location never depends on the process working directory.
/// </summary>
internal sealed class LocalFileStorage : IFileStorage
{
    private readonly string _rootPath;

    public LocalFileStorage(IOptions<LocalFileStorageOptions> options, IHostEnvironment hostEnvironment)
    {
        _rootPath = LocalFileStorageOptions.ResolveRootPath(options.Value.RootPath, hostEnvironment.ContentRootPath);
    }

    public async Task<string> SaveAsync(Stream content, string contentType, string originalFileName, CancellationToken cancellationToken = default)
    {
        _ = contentType;
        var id = Guid.NewGuid().ToString("N");
        var safeName = SanitizeFileName(originalFileName);
        var relative = $"{id[..2]}/{id[2..4]}/{id}-{safeName}";
        var full = Path.Combine(_rootPath, relative);

        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        await using var stream = File.Create(full);
        await content.CopyToAsync(stream, cancellationToken);

        // Always return POSIX-style keys so consumers can store/transport them verbatim.
        return relative.Replace('\\', '/');
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var full = ResolveAndValidate(storageKey);
        Stream stream = File.OpenRead(full);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var full = ResolveAndValidate(storageKey);
        if (File.Exists(full))
            File.Delete(full);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Builds the full path for a storage key and guarantees it stays within
    /// the configured root so a crafted key cannot escape via <c>..</c>.
    /// </summary>
    private string ResolveAndValidate(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("Storage key is required.", nameof(storageKey));

        var normalized = storageKey.Replace('\\', '/').TrimStart('/');
        var full = Path.GetFullPath(Path.Combine(_rootPath, normalized));
        var rootWithSep = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        if (!full.StartsWith(rootWithSep, StringComparison.Ordinal) && full != _rootPath)
            throw new UnauthorizedAccessException("Storage key points outside of the configured root.");

        return full;
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name)) name = "file";
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        // Defensive: cap length to avoid filesystem limits.
        if (cleaned.Length > 150) cleaned = cleaned[..150];
        return cleaned;
    }
}
