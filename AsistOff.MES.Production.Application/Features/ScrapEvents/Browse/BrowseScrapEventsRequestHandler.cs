using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ScrapEvents.Browse;

internal sealed class BrowseScrapEventsRequestHandler(IScrapEventsRepository repository)
    : IRequestHandler<BrowseScrapEventsRequest, PagedResponse<ScrapEventResponse>>
{
    public async Task<PagedResponse<ScrapEventResponse>> Handle(BrowseScrapEventsRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<ScrapEvent>(true);

        if (request.MachineId.HasValue)
            predicate = predicate.And(x => x.MachineId == request.MachineId.Value);
        if (request.ReasonCodeId.HasValue)
            predicate = predicate.And(x => x.ReasonCodeId == request.ReasonCodeId.Value);
        if (request.ReportedFrom.HasValue)
            predicate = predicate.And(x => x.ReportedAt >= request.ReportedFrom.Value.ToUniversalTime());
        if (request.ReportedTo.HasValue)
            predicate = predicate.And(x => x.ReportedAt <= request.ReportedTo.Value.ToUniversalTime());

        var totalCount = await repository.CountAsync(predicate, cancellationToken);
        var paginator = new Paginator<ScrapEvent>(predicate, request);
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        return new PagedScrapEventsResponse(
            items.Select(Map).ToList(), totalCount, request.PageSize);
    }

    internal static ScrapEventResponse Map(ScrapEvent e) => new(
        e.Id, e.MachineId, e.ReasonCodeId, e.Quantity, e.ReportedAt, e.Notes,
        e.ReportedByOperatorId, e.ProductionOrderId, e.CreatedAt, e.UpdatedAt);
}
