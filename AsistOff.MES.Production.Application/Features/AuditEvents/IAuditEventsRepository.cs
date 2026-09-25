namespace AsistOff.MES.Production.Application.Features.AuditEvents;

/// <summary>
/// Read-side contract for the append-only audit history. Tenant isolation
/// rides on the EF Core global query filter in the implementation — no manual
/// <c>TenantId</c> predicates. History rows are written by the
/// <c>AuditHistoryInterceptor</c> in the primary write transaction, never here.
/// </summary>
public interface IAuditEventsRepository
{
    Task<int> CountAsync(string? entityName, Guid? entityId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditEventResponse>> BrowseAsync(
        string? entityName,
        Guid? entityId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}
