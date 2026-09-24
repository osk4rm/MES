using AsistOff.MES.Production.Application.Features.SpcCharacteristics.Responses;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.SpcCharacteristics.Browse;

internal sealed class BrowseSpcCharacteristicsRequestHandler(ISpcCharacteristicsRepository repository)
    : IRequestHandler<BrowseSpcCharacteristicsRequest, PagedResponse<SpcCharacteristicResponse>>
{
    public async Task<PagedResponse<SpcCharacteristicResponse>> Handle(
        BrowseSpcCharacteristicsRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<SpcCharacteristic>(true);

        if (!string.IsNullOrWhiteSpace(request.Search))
            predicate = predicate.And(x => x.Code.Contains(request.Search) || x.Name.Contains(request.Search));
        if (request.ProductId.HasValue)
            predicate = predicate.And(x => x.ProductId == request.ProductId);
        if (request.MachineId.HasValue)
            predicate = predicate.And(x => x.MachineId == request.MachineId);
        if (request.IsActive.HasValue)
            predicate = predicate.And(x => x.IsActive == request.IsActive);

        var totalCount = await repository.CountAsync(predicate, cancellationToken);
        var paginator = new Paginator<SpcCharacteristic>(predicate, request);
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        var result = items.Select(Map).ToList();
        return new PagedSpcCharacteristicsResponse(result, totalCount, request.PageSize);
    }

    internal static SpcCharacteristicResponse Map(SpcCharacteristic c) =>
        new(c.Id, c.Code, c.Name, c.Description, c.ProductId, c.MachineId, c.ChartType,
            c.NominalValue, c.LowerSpecLimit, c.UpperSpecLimit,
            c.LowerControlLimit, c.UpperControlLimit, c.SampleSize, c.Unit, c.IsActive);
}
