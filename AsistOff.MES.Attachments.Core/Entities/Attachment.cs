using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Attachments.Domain.Entities;

/// <summary>
/// Polymorphic attachment — links to any owner via <see cref="OwnerType"/> + <see cref="OwnerId"/>.
/// Binary content is not kept in the database; it is stored through <c>IFileStorage</c>
/// and referenced by <see cref="StorageKey"/>.
/// </summary>
public class Attachment : IEntity, ISaasy, IAuditable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public required string OwnerType { get; set; }
    public Guid OwnerId { get; set; }

    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public required string StorageKey { get; set; }

    public string? Description { get; set; }
    public Guid? UploadedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
