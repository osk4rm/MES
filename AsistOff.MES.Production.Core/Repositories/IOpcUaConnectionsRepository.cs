using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface IOpcUaConnectionsRepository
{
    Task<IReadOnlyCollection<OpcUaConnection>> BrowseAsync(Paginator<OpcUaConnection> paginator, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<OpcUaConnection> predicate, CancellationToken cancellationToken = default);
    Task<OpcUaConnection?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OpcUaConnection?> GetByEndpointAsync(Guid machineId, string endpointUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// All connections of the ambient tenant (the global query filter
    /// scopes the result). Used by the connection-status query.
    /// </summary>
    Task<IReadOnlyCollection<OpcUaConnection>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enabled connections of the ambient tenant (the global query filter
    /// scopes the result). Used by the OPC UA poller.
    /// </summary>
    Task<IReadOnlyCollection<OpcUaConnection>> ListEnabledAsync(CancellationToken cancellationToken = default);
    Task<OpcUaConnection> AddAsync(OpcUaConnection entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(OpcUaConnection entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
