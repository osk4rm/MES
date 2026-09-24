using AsistOff.MES.Production.Application.Features.TelemetryReadings.Browse;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.TelemetryReadings.Export;

internal sealed class ExportTelemetryReadingsRequestHandler(ITelemetryReadingsRepository repository)
    : IRequestHandler<ExportTelemetryReadingsRequest, string>
{
    public async Task<string> Handle(ExportTelemetryReadingsRequest request, CancellationToken cancellationToken)
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

        var entities = await repository.BrowseExportAsync(
            predicate, request.LatestOnly, TelemetryReadingCsvFormatter.MaxRows, cancellationToken);

        return TelemetryReadingCsvFormatter.Format(
            entities.Select(BrowseTelemetryReadingsRequestHandler.Map));
    }
}
