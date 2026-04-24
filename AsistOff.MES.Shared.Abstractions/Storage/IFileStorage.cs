namespace AsistOff.MES.Shared.Abstractions.Storage;

/// <summary>
/// Abstract file storage. Implementations may store blobs on the local filesystem,
/// in AWS S3, Azure Blob Storage or any other backend — consumers should only
/// rely on the returned <see cref="string"/> storage key, which is an opaque
/// handle managed by the provider.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Persists the supplied content and returns a provider-specific opaque key
    /// used to retrieve or delete the blob later.
    /// </summary>
    /// <param name="content">A stream positioned at the start of the payload.</param>
    /// <param name="contentType">MIME type (e.g. <c>application/pdf</c>).</param>
    /// <param name="originalFileName">The original filename supplied by the uploader.</param>
    Task<string> SaveAsync(Stream content, string contentType, string originalFileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a readable stream for the given storage key. Caller is responsible for disposing the stream.
    /// </summary>
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the blob identified by the given key. No-op if the blob does not exist.
    /// </summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
