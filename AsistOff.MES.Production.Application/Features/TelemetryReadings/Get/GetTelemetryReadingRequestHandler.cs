using AsistOff.MES.Production.Application.Features.TelemetryReadings.Browse;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.TelemetryReadings.Get;

internal sealed class GetTelemetryReadingRequestHandler(ITelemetryReadingsRepository repository)
    : IRequestHandler<GetTelemetryReadingRequest, TelemetryReadingResponse>
{
    public async Task<TelemetryReadingResponse> Handle(GetTelemetryReadingRequest request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("TelemetryReading", request.Id);

        return BrowseTelemetryReadingsRequestHandler.Map(entity);
    }
}
