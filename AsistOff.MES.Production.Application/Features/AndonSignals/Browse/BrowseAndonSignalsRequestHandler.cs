using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.AndonSignals.Browse;

internal sealed class BrowseAndonSignalsRequestHandler(IAndonSignalsRepository repository)
    : IRequestHandler<BrowseAndonSignalsRequest, PagedResponse<AndonSignalResponse>>
{
    public async Task<PagedResponse<AndonSignalResponse>> Handle(BrowseAndonSignalsRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<AndonSignal>(true);

        if (request.MachineId.HasValue)
            predicate = predicate.And(x => x.MachineId == request.MachineId);
        if (request.Category.HasValue)
            predicate = predicate.And(x => x.Category == request.Category);
        if (request.Status.HasValue)
            predicate = predicate.And(x => x.Status == request.Status);
        if (request.RaisedFrom.HasValue)
            predicate = predicate.And(x => x.RaisedAt >= request.RaisedFrom);
        if (request.RaisedTo.HasValue)
            predicate = predicate.And(x => x.RaisedAt <= request.RaisedTo);

        var totalCount = await repository.CountAsync(predicate, cancellationToken);
        var paginator = new Paginator<AndonSignal>(predicate, request);
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        return new PagedAndonSignalsResponse(
            items.Select(Map).ToList(), totalCount, request.PageSize);
    }

    internal static AndonSignalResponse Map(AndonSignal s) => new(
        s.Id, s.MachineId, s.Category, s.ReasonCodeId, s.Status,
        s.RaisedAt, s.AcknowledgedAt, s.ResolvedAt, s.Notes,
        s.RaisedByOperatorId, s.ProductionOrderId, s.CreatedAt, s.UpdatedAt);
}
