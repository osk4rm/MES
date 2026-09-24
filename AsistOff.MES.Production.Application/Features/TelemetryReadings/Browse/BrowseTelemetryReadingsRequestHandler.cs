using AsistOff.MES.Production.Application.Features.TelemetryReadings;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.TelemetryReadings.Browse;

internal sealed class BrowseTelemetryReadingsRequestHandler(ITelemetryReadingsRepository repository)
    : IRequestHandler<BrowseTelemetryReadingsRequest, PagedResponse<TelemetryReadingResponse>>
{
    public async Task<PagedResponse<TelemetryReadingResponse>> Handle(BrowseTelemetryReadingsRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<TelemetryReading>(true);

        if (request.TagId.HasValue)
            predicate = predicate.And(x => x.TagId == request.TagId.Value);
        if (request.MachineId.HasValue)
            predicate = predicate.And(x => x.MachineId == request.MachineId.Value);
        if (request.ReadAtFrom.HasValue)
            predicate = predicate.And(x => x.ReadAt >= request.ReadAtFrom.Value);
        if (request.ReadAtTo.HasValue)
            predicate = predicate.And(x => x.ReadAt <= request.ReadAtTo.Value);

        var paginator = new Paginator<TelemetryReading>(predicate, request);

        int totalCount;
        IReadOnlyCollection<TelemetryReading> items;
        if (request.LatestOnly)
        {
            totalCount = await repository.CountLatestAsync(predicate, cancellationToken);
            items = await repository.BrowseLatestAsync(predicate, paginator, cancellationToken);
        }
        else
        {
            totalCount = await repository.CountAsync(predicate, cancellationToken);
            items = await repository.BrowseAsync(paginator, cancellationToken);
        }

        return new PagedTelemetryReadingsResponse(
            items.Select(Map).ToList(), totalCount, request.PageSize);
    }

    internal static TelemetryReadingResponse Map(TelemetryReading e) => new(
        e.Id, e.TagId, e.MachineId, e.ReadAt,
        e.DoubleValue, e.StringValue, e.Quality);
}
