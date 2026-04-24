using AsistOff.MES.Attachments.Domain.Entities;

namespace AsistOff.MES.Attachments.Domain.Repositories;

public interface IAttachmentsRepository
{
    Task<Attachment?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Attachment>> ListForOwnerAsync(string ownerType, Guid ownerId, CancellationToken cancellationToken = default);
    Task<Attachment> AddAsync(Attachment attachment, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
