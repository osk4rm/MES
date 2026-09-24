using AsistOff.MES.Production.Application.Features.MachineTelemetryTags;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Browse;

internal sealed class BrowseMachineTelemetryTagsRequestHandler(IMachineTelemetryTagsRepository repository)
    : IRequestHandler<BrowseMachineTelemetryTagsRequest, PagedResponse<MachineTelemetryTagResponse>>
{
    public async Task<PagedResponse<MachineTelemetryTagResponse>> Handle(BrowseMachineTelemetryTagsRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<MachineTelemetryTag>(true);

        if (request.MachineId.HasValue)
            predicate = predicate.And(x => x.MachineId == request.MachineId.Value);
        if (request.IsEnabled.HasValue)
            predicate = predicate.And(x => x.IsEnabled == request.IsEnabled.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            predicate = predicate.And(x => x.NodeId.Contains(search) || x.DisplayName.Contains(search));
        }

        var totalCount = await repository.CountAsync(predicate, cancellationToken);
        var paginator = new Paginator<MachineTelemetryTag>(predicate, request);
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        return new PagedMachineTelemetryTagsResponse(
            items.Select(Map).ToList(), totalCount, request.PageSize);
    }

    internal static MachineTelemetryTagResponse Map(MachineTelemetryTag e) => new(
        e.Id, e.MachineId, e.NodeId, e.DisplayName, e.DataType,
        e.PollIntervalSeconds, e.IsEnabled, e.Description,
        e.CreatedAt, e.UpdatedAt);
}
