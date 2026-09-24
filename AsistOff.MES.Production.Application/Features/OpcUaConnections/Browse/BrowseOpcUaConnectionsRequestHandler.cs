using AsistOff.MES.Production.Application.Features.OpcUaConnections;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections.Browse;

internal sealed class BrowseOpcUaConnectionsRequestHandler(IOpcUaConnectionsRepository repository)
    : IRequestHandler<BrowseOpcUaConnectionsRequest, PagedResponse<OpcUaConnectionResponse>>
{
    public async Task<PagedResponse<OpcUaConnectionResponse>> Handle(BrowseOpcUaConnectionsRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<OpcUaConnection>(true);

        if (request.MachineId.HasValue)
            predicate = predicate.And(x => x.MachineId == request.MachineId.Value);
        if (request.IsEnabled.HasValue)
            predicate = predicate.And(x => x.IsEnabled == request.IsEnabled.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            predicate = predicate.And(x => x.EndpointUrl.Contains(search));
        }

        var totalCount = await repository.CountAsync(predicate, cancellationToken);
        var paginator = new Paginator<OpcUaConnection>(predicate, request);
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        return new PagedOpcUaConnectionsResponse(
            items.Select(Map).ToList(), totalCount, request.PageSize);
    }

    internal static OpcUaConnectionResponse Map(OpcUaConnection e) => new(
        e.Id, e.MachineId, e.EndpointUrl, e.SecurityPolicy,
        e.PollIntervalSeconds, e.IsEnabled, e.LastSeenAtUtc, e.LastError,
        e.CreatedAt, e.UpdatedAt);
}
