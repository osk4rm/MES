using AsistOff.MES.Attachments.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Storage;
using MediatR;

namespace AsistOff.MES.Attachments.Application.Features.Delete;

public record DeleteAttachmentRequest(Guid Id) : ITenantRequest;

internal sealed class DeleteAttachmentRequestHandler(
    IAttachmentsRepository repository,
    IFileStorage storage)
    : IRequestHandler<DeleteAttachmentRequest>
{
    public async Task Handle(DeleteAttachmentRequest request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Attachment", request.Id);

        await repository.DeleteAsync(entity.Id, cancellationToken);
        try
        {
            await storage.DeleteAsync(entity.StorageKey, cancellationToken);
        }
        catch
        {
            // Metadata is already gone; a missed blob delete is non-fatal and
            // can be cleaned up by a background GC later.
        }
    }
}
