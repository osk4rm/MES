using AsistOff.MES.Attachments.Application.Features.Common;
using AsistOff.MES.Attachments.Application.Features.Responses;
using AsistOff.MES.Attachments.Domain.Entities;
using AsistOff.MES.Attachments.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Shared.Abstractions.Storage;
using MediatR;
using Microsoft.Extensions.Options;

namespace AsistOff.MES.Attachments.Application.Features.Upload;

internal sealed class UploadAttachmentRequestHandler(
    IAttachmentsRepository repository,
    IFileStorage storage,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext,
    IOptions<AttachmentUploadOptions> uploadOptions,
    IAttachmentOwnerVerifier ownerVerifier)
    : IRequestHandler<UploadAttachmentRequest, AttachmentResponse>
{
    public async Task<AttachmentResponse> Handle(UploadAttachmentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OwnerType))
            throw new ValidationException(nameof(request.OwnerType), "Owner type is required");
        if (string.IsNullOrWhiteSpace(request.FileName))
            throw new ValidationException(nameof(request.FileName), "File name is required");
        if (request.SizeBytes <= 0)
            throw new ValidationException(nameof(request.SizeBytes), "Attachment cannot be empty");
        if (request.Content is null)
            throw new ValidationException("file", "File is required.");

        var options = uploadOptions.Value;

        if (request.SizeBytes > options.MaxFileSizeBytes)
            throw new ValidationException(nameof(request.SizeBytes), $"Attachment exceeds the maximum size of {options.MaxFileSizeBytes} bytes.");

        // Buffer the payload (bounded by the max size) so allowlist, sniffing
        // and size checks all run before anything is persisted.
        var content = await BufferAsync(request.Content, options.MaxFileSizeBytes, cancellationToken);

        var normalizedContentType = AttachmentUploadGuard.EnsureAllowed(request.FileName, request.ContentType, options);
        AttachmentContentSniffer.EnsureMatches(normalizedContentType, content.Span);

        if (!await ownerVerifier.ExistsAsync(request.OwnerType, request.OwnerId, cancellationToken))
            throw new NotFoundException("Owner", request.OwnerId);

        var safeFileName = AttachmentFileNameSanitizer.Sanitize(request.FileName);

        using var stream = new MemoryStream(content.ToArray());
        var storageKey = await storage.SaveAsync(stream, normalizedContentType, safeFileName, cancellationToken);

        var entity = new Attachment
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            OwnerType = request.OwnerType,
            OwnerId = request.OwnerId,
            FileName = safeFileName,
            ContentType = normalizedContentType,
            SizeBytes = content.Length,
            StorageKey = storageKey,
            Description = request.Description,
            CreatedAt = dateTimeProvider.UtcNow
        };

        try
        {
            await repository.AddAsync(entity, cancellationToken);
        }
        catch
        {
            // Roll back the blob if the metadata write fails to avoid orphans.
            try { await storage.DeleteAsync(storageKey, CancellationToken.None); } catch { /* best-effort */ }
            throw;
        }

        return new AttachmentResponse(
            entity.Id, entity.OwnerType, entity.OwnerId, entity.FileName, entity.ContentType,
            entity.SizeBytes, entity.Description, entity.CreatedAt, entity.UploadedByUserId);
    }

    private static async Task<ReadOnlyMemory<byte>> BufferAsync(Stream content, long maxBytes, CancellationToken cancellationToken)
    {
        // Stream may be non-seekable (multipart); copy up to max+1 to detect overflow.
        using var buffered = new MemoryStream();
        var remaining = maxBytes + 1;
        var chunk = new byte[81920];
        int read;
        while (remaining > 0 && (read = await content.ReadAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, remaining)), cancellationToken)) != 0)
        {
            buffered.Write(chunk, 0, read);
            remaining -= read;
        }

        if (remaining == 0)
            throw new ValidationException("file", $"Attachment exceeds the maximum size of {maxBytes} bytes.");

        if (buffered.Length == 0)
            throw new ValidationException("file", "Attachment cannot be empty.");

        return buffered.ToArray();
    }
}
