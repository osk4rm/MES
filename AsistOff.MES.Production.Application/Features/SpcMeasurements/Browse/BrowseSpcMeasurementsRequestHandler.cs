using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.SpcMeasurements.Browse;

internal sealed class BrowseSpcMeasurementsRequestHandler(ISpcMeasurementsRepository repository)
    : IRequestHandler<BrowseSpcMeasurementsRequest, PagedResponse<SpcMeasurementResponse>>
{
    public async Task<PagedResponse<SpcMeasurementResponse>> Handle(BrowseSpcMeasurementsRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<SpcMeasurement>(true);

        if (request.CharacteristicId.HasValue)
            predicate = predicate.And(x => x.CharacteristicId == request.CharacteristicId.Value);
        if (request.From.HasValue)
            predicate = predicate.And(x => x.MeasuredAt >= request.From.Value.ToUniversalTime());
        if (request.To.HasValue)
            predicate = predicate.And(x => x.MeasuredAt <= request.To.Value.ToUniversalTime());

        var totalCount = await repository.CountAsync(predicate, cancellationToken);
        var paginator = new Paginator<SpcMeasurement>(predicate, request);
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        return new PagedSpcMeasurementsResponse(
            items.Select(Map).ToList(), totalCount, request.PageSize);
    }

    internal static SpcMeasurementResponse Map(SpcMeasurement e) => new(
        e.Id, e.CharacteristicId, e.Value, e.MeasuredAt, e.Notes,
        e.CreatedAt, e.UpdatedAt);
}
