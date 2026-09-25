using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.LotGenealogy.Browse;

internal sealed class BrowseLotGenealogyEdgesRequestHandler(ILotGenealogyEdgesRepository repository)
    : IRequestHandler<BrowseLotGenealogyEdgesRequest, PagedResponse<LotGenealogyEdgeResponse>>
{
    public async Task<PagedResponse<LotGenealogyEdgeResponse>> Handle(BrowseLotGenealogyEdgesRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<LotGenealogyEdge>(true);

        if (request.ProducedLotId.HasValue)
            predicate = predicate.And(x => x.ProducedLotId == request.ProducedLotId.Value);
        if (request.ConsumedLotId.HasValue)
            predicate = predicate.And(x => x.ConsumedLotId == request.ConsumedLotId.Value);
        if (request.ProductionOrderId.HasValue)
            predicate = predicate.And(x => x.ProductionOrderId == request.ProductionOrderId.Value);
        if (request.ProductionConfirmationId.HasValue)
            predicate = predicate.And(x => x.ProductionConfirmationId == request.ProductionConfirmationId.Value);
        if (request.From.HasValue)
            predicate = predicate.And(x => x.OccurredAt >= request.From.Value.ToUniversalTime());
        if (request.To.HasValue)
            predicate = predicate.And(x => x.OccurredAt <= request.To.Value.ToUniversalTime());

        var totalCount = await repository.CountAsync(predicate, cancellationToken);
        var paginator = new Paginator<LotGenealogyEdge>(predicate, request);
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        return new PagedLotGenealogyEdgesResponse(
            items.Select(Map).ToList(), totalCount, request.PageSize);
    }

    internal static LotGenealogyEdgeResponse Map(LotGenealogyEdge e) => new(
        e.Id, e.ConsumedLotId, e.ProducedLotId, e.ProductionOrderId,
        e.ProductionConfirmationId, e.MachineId, e.ReportedByOperatorId,
        e.ConsumedQuantity, e.OccurredAt, e.Notes,
        e.CreatedAt, e.UpdatedAt);
}
