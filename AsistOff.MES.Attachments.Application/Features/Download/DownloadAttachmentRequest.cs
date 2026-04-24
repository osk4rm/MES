using AsistOff.MES.Attachments.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Storage;
using MediatR;

namespace AsistOff.MES.Attachments.Application.Features.Download;

public record DownloadAttachmentRequest(Guid Id)
    : ITenantRequest<DownloadAttachmentResponse>;

public record DownloadAttachmentResponse(
    Stream Content,
    string FileName,
    string ContentType,
    long SizeBytes);

internal sealed class DownloadAttachmentRequestHandler(
    IAttachmentsRepository repository,
    IFileStorage storage)
    : IRequestHandler<DownloadAttachmentRequest, DownloadAttachmentResponse>
{
    public async Task<DownloadAttachmentResponse> Handle(DownloadAttachmentRequest request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Attachment", request.Id);

        var stream = await storage.OpenReadAsync(entity.StorageKey, cancellationToken);
        return new DownloadAttachmentResponse(stream, entity.FileName, entity.ContentType, entity.SizeBytes);
    }
}
