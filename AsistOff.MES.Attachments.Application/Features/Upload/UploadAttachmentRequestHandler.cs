using AsistOff.MES.Attachments.Application.Features.Responses;
using AsistOff.MES.Attachments.Domain.Entities;
using AsistOff.MES.Attachments.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Shared.Abstractions.Storage;
using MediatR;

namespace AsistOff.MES.Attachments.Application.Features.Upload;

internal sealed class UploadAttachmentRequestHandler(
    IAttachmentsRepository repository,
    IFileStorage storage,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext)
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

        var storageKey = await storage.SaveAsync(request.Content, request.ContentType, request.FileName, cancellationToken);

        var entity = new Attachment
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            OwnerType = request.OwnerType,
            OwnerId = request.OwnerId,
            FileName = request.FileName,
            ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType,
            SizeBytes = request.SizeBytes,
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
}
