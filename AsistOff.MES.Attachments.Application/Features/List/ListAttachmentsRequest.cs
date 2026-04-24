using AsistOff.MES.Attachments.Application.Features.Responses;
using AsistOff.MES.Attachments.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using MediatR;

namespace AsistOff.MES.Attachments.Application.Features.List;

public record ListAttachmentsRequest(string OwnerType, Guid OwnerId)
    : ITenantRequest<IReadOnlyCollection<AttachmentResponse>>;

internal sealed class ListAttachmentsRequestHandler(IAttachmentsRepository repository)
    : IRequestHandler<ListAttachmentsRequest, IReadOnlyCollection<AttachmentResponse>>
{
    public async Task<IReadOnlyCollection<AttachmentResponse>> Handle(ListAttachmentsRequest request, CancellationToken cancellationToken)
    {
        var items = await repository.ListForOwnerAsync(request.OwnerType, request.OwnerId, cancellationToken);
        return items.Select(x => new AttachmentResponse(
            x.Id, x.OwnerType, x.OwnerId, x.FileName, x.ContentType,
            x.SizeBytes, x.Description, x.CreatedAt, x.UploadedByUserId)).ToList();
    }
}
