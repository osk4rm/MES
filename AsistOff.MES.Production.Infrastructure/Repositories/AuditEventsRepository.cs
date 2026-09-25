using AsistOff.MES.Production.Application.Features.AuditEvents;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using AsistOff.MES.Shared.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Production.Infrastructure.Repositories;

/// <summary>
/// Read-side repository for the append-only audit history. Tenant isolation
/// rides on the EF Core global query filter — no manual <c>TenantId</c>
/// predicates and no <c>IgnoreQueryFilters</c> bypass, so cross-tenant browses
/// return empty results. Rows are ordered newest-first for timeline reads.
/// </summary>
internal sealed class AuditEventsRepository(DefaultContext context) : IAuditEventsRepository
{
    public Task<int> CountAsync(string? entityName, Guid? entityId, CancellationToken cancellationToken = default)
        => ApplyFilter(context.Set<AuditEvent>(), entityName, entityId)
            .CountAsync(cancellationToken);

    public async Task<IReadOnlyList<AuditEventResponse>> BrowseAsync(
        string? entityName,
        Guid? entityId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var rows = await ApplyFilter(context.Set<AuditEvent>(), entityName, entityId)
            .AsNoTracking()
            .OrderByDescending(x => x.ChangedAt)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows
            .Select(x => new AuditEventResponse(
                x.Id,
                x.EntityName,
                x.EntityId,
                (short)x.Action,
                x.ChangedAt,
                x.ActorId,
                x.Payload))
            .ToList();
    }

    private static IQueryable<AuditEvent> ApplyFilter(
        IQueryable<AuditEvent> query,
        string? entityName,
        Guid? entityId)
    {
        if (!string.IsNullOrWhiteSpace(entityName))
        {
            query = query.Where(x => x.EntityName == entityName);
        }

        if (entityId.HasValue)
        {
            query = query.Where(x => x.EntityId == entityId.Value);
        }

        return query;
    }
}
