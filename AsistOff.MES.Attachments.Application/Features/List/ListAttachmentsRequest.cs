using AsistOff.MES.Attachments.Application.Features.Common;
using AsistOff.MES.Attachments.Application.Features.Responses;
using AsistOff.MES.Attachments.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Users.Core.Rbac;
using MediatR;

namespace AsistOff.MES.Attachments.Application.Features.List;

[RequirePermission(RbacDefaults.AttachmentsRead)]
public record ListAttachmentsRequest(string OwnerType, Guid OwnerId)
    : ITenantRequest<IReadOnlyCollection<AttachmentResponse>>;

internal sealed class ListAttachmentsRequestHandler(
    IAttachmentsRepository repository,
    IAttachmentOwnerVerifier ownerVerifier,
    ICurrentPermissionsAccessor permissionsAccessor)
    : IRequestHandler<ListAttachmentsRequest, IReadOnlyCollection<AttachmentResponse>>
{
    public async Task<IReadOnlyCollection<AttachmentResponse>> Handle(ListAttachmentsRequest request, CancellationToken cancellationToken)
    {
        if (!await ownerVerifier.ExistsAsync(request.OwnerType, request.OwnerId, cancellationToken))
            throw new NotFoundException("Owner", request.OwnerId);

        // Object-level rule (issue #315): the caller must also hold the
        // owner-module read permission; out-of-scope owners are denied here
        // before any attachment rows are read.
        AttachmentScopePolicy.EnsureScopeAccess(request.OwnerType, permissionsAccessor.Permissions);

        var items = await repository.ListForOwnerAsync(request.OwnerType, request.OwnerId, cancellationToken);
        return items.Select(x => new AttachmentResponse(
            x.Id, x.OwnerType, x.OwnerId, x.FileName, x.ContentType,
            x.SizeBytes, x.Description, x.CreatedAt, x.UploadedByUserId)).ToList();
    }
}
