using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Application.Features.OpcUaConnections;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections.Browse;

internal sealed class BrowseOpcUaConnectionsRequestHandler(
    IOpcUaConnectionsRepository repository,
    ICurrentPermissionsAccessor permissionsAccessor)
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

        var responses = items.Select(Map).ToList();

        // Issue #372: the raw provider error can leak endpoint topology or
        // network fragments, so read-only callers get null while liveness
        // flags stay visible. Create/toggle responses keep the full value —
        // those requests require production.write, hence privileged callers.
        if (!SensitiveProjectionPolicy.CanSeeSensitiveDetails(permissionsAccessor.Permissions))
            responses = responses.Select(x => x with { LastError = null }).ToList();

        return new PagedOpcUaConnectionsResponse(responses, totalCount, request.PageSize);
    }

    internal static OpcUaConnectionResponse Map(OpcUaConnection e) => new(
        e.Id, e.MachineId, e.EndpointUrl, e.SecurityPolicy,
        e.PollIntervalSeconds, e.IsEnabled, e.LastSeenAtUtc, e.LastError,
        e.CreatedAt, e.UpdatedAt);
}
