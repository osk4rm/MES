using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ProductionConfirmations.Browse;

internal sealed class BrowseProductionConfirmationsRequestHandler(IProductionConfirmationsRepository repository)
    : IRequestHandler<BrowseProductionConfirmationsRequest, PagedResponse<ProductionConfirmationResponse>>
{
    public async Task<PagedResponse<ProductionConfirmationResponse>> Handle(BrowseProductionConfirmationsRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<ProductionConfirmation>(true);

        if (request.ProductionOrderId.HasValue)
            predicate = predicate.And(x => x.ProductionOrderId == request.ProductionOrderId.Value);
        if (request.MachineId.HasValue)
            predicate = predicate.And(x => x.MachineId == request.MachineId.Value);
        if (request.From.HasValue)
            predicate = predicate.And(x => x.ReportedAt >= request.From.Value.ToUniversalTime());
        if (request.To.HasValue)
            predicate = predicate.And(x => x.ReportedAt <= request.To.Value.ToUniversalTime());

        var totalCount = await repository.CountAsync(predicate, cancellationToken);
        var paginator = new Paginator<ProductionConfirmation>(predicate, request);
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        return new PagedProductionConfirmationsResponse(
            items.Select(Map).ToList(), totalCount, request.PageSize);
    }

    internal static ProductionConfirmationResponse Map(ProductionConfirmation e) => new(
        e.Id, e.ProductionOrderId, e.MachineId, e.ReportedByOperatorId,
        e.ReportedAt, e.GoodQuantity, e.ScrapQuantity, e.Notes,
        e.CreatedAt, e.UpdatedAt);
}
