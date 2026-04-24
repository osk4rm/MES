using AsistOff.MES.Attachments.Application.Features.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Attachments.Application.Features.Upload;

public record UploadAttachmentRequest(
    string OwnerType,
    Guid OwnerId,
    string FileName,
    string ContentType,
    long SizeBytes,
    Stream Content,
    string? Description) : ITenantRequest<AttachmentResponse>;
