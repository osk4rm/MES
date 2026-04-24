namespace AsistOff.MES.Attachments.Application.Features.Responses;

public record AttachmentResponse(
    Guid Id,
    string OwnerType,
    Guid OwnerId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? Description,
    DateTime CreatedAt,
    Guid? UploadedByUserId);
