using AsistOff.MES.Attachments.Application.Features.Common;
using AsistOff.MES.Attachments.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Storage;
using AsistOff.MES.Users.Core.Rbac;
using MediatR;

namespace AsistOff.MES.Attachments.Application.Features.Download;

[RequirePermission(RbacDefaults.AttachmentsRead)]
public record DownloadAttachmentRequest(Guid Id)
    : ITenantRequest<DownloadAttachmentResponse>;

public record DownloadAttachmentResponse(
    Stream Content,
    string FileName,
    string ContentType,
    long SizeBytes);

internal sealed class DownloadAttachmentRequestHandler(
    IAttachmentsRepository repository,
    IFileStorage storage,
    ICurrentPermissionsAccessor permissionsAccessor)
    : IRequestHandler<DownloadAttachmentRequest, DownloadAttachmentResponse>
{
    public async Task<DownloadAttachmentResponse> Handle(DownloadAttachmentRequest request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Attachment", request.Id);

        // Object-level rule (issue #315): deny out-of-scope owners before
        // opening storage so no file bytes are streamed to an unauthorized
        // caller.
        AttachmentScopePolicy.EnsureScopeAccess(entity.OwnerType, permissionsAccessor.Permissions);

        var stream = await storage.OpenReadAsync(entity.StorageKey, cancellationToken);
        return new DownloadAttachmentResponse(stream, entity.FileName, entity.ContentType, entity.SizeBytes);
    }
}
