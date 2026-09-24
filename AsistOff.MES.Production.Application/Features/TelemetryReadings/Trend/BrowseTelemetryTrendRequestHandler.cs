using AsistOff.MES.Production.Application.Features.TelemetryReadings.Browse;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.TelemetryReadings.Trend;

internal sealed class BrowseTelemetryTrendRequestHandler(
    IMachineTelemetryTagsRepository tagsRepository,
    ITelemetryReadingsRepository readingsRepository)
    : IRequestHandler<BrowseTelemetryTrendRequest, IReadOnlyList<TelemetryReadingResponse>>
{
    public async Task<IReadOnlyList<TelemetryReadingResponse>> Handle(BrowseTelemetryTrendRequest request, CancellationToken cancellationToken)
    {
        if (request.Take is < 1 or > 200)
            throw new ValidationException(nameof(request.Take), "Take must be between 1 and 200.");

        // The global tenant query filter scopes the tag lookup to the
        // caller tenant, so unknown and cross-tenant ids both yield 404.
        _ = await tagsRepository.GetAsync(request.TagId, cancellationToken)
            ?? throw new NotFoundException("MachineTelemetryTag", request.TagId);

        var readings = await readingsRepository.BrowseTrendAsync(request.TagId, request.Take, cancellationToken);

        return readings.Select(BrowseTelemetryReadingsRequestHandler.Map).ToList();
    }
}
