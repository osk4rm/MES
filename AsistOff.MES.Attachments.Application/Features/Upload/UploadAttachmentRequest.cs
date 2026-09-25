using AsistOff.MES.Attachments.Application.Features.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Attachments.Application.Features.Upload;

[RequirePermission(RbacDefaults.AttachmentsWrite)]
public record UploadAttachmentRequest(
    string OwnerType,
    Guid OwnerId,
    string FileName,
    string ContentType,
    long SizeBytes,
    Stream Content,
    string? Description) : ITenantRequest<AttachmentResponse>;
