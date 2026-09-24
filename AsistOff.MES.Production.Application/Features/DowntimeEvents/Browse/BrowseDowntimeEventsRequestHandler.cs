using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.DowntimeEvents.Browse;

internal sealed class BrowseDowntimeEventsRequestHandler(IDowntimeEventsRepository repository)
    : IRequestHandler<BrowseDowntimeEventsRequest, PagedResponse<DowntimeEventResponse>>
{
    public async Task<PagedResponse<DowntimeEventResponse>> Handle(BrowseDowntimeEventsRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<DowntimeEvent>(true);

        if (request.MachineId.HasValue)
            predicate = predicate.And(x => x.MachineId == request.MachineId.Value);
        if (request.ReasonCodeId.HasValue)
            predicate = predicate.And(x => x.ReasonCodeId == request.ReasonCodeId.Value);
        if (request.Status.HasValue)
        {
            predicate = request.Status.Value == DowntimeEventStatus.Open
                ? predicate.And(x => x.EndedAt == null)
                : predicate.And(x => x.EndedAt != null);
        }
        if (request.StartedFrom.HasValue)
            predicate = predicate.And(x => x.StartedAt >= request.StartedFrom.Value);
        if (request.StartedTo.HasValue)
            predicate = predicate.And(x => x.StartedAt <= request.StartedTo.Value);

        var totalCount = await repository.CountAsync(predicate, cancellationToken);
        var paginator = new Paginator<DowntimeEvent>(predicate, request);
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        return new PagedDowntimeEventsResponse(
            items.Select(Map).ToList(), totalCount, request.PageSize);
    }

    internal static DowntimeEventResponse Map(DowntimeEvent e) => new(
        e.Id, e.MachineId, e.ReasonCodeId, e.StartedAt, e.EndedAt, e.Status,
        e.DurationMinutes, e.Notes, e.ReportedByOperatorId, e.ProductionOrderId,
        e.CreatedAt, e.UpdatedAt);
}
