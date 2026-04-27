using AsistOff.MES.Attachments.Domain.Entities;
using AsistOff.MES.Attachments.Domain.Repositories;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Attachments.Infrastructure.Repositories;

internal sealed class AttachmentsRepository(DefaultContext context) : IAttachmentsRepository
{
    public Task<Attachment?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<Attachment>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<Attachment>> ListForOwnerAsync(string ownerType, Guid ownerId, CancellationToken cancellationToken = default)
        => await context.Set<Attachment>()
            .Where(x => x.OwnerType == ownerType && x.OwnerId == ownerId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<Attachment> AddAsync(Attachment attachment, CancellationToken cancellationToken = default)
    {
        context.Set<Attachment>().Add(attachment);
        await context.SaveChangesAsync(cancellationToken);
        return attachment;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<Attachment>().Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken);
    }
}
