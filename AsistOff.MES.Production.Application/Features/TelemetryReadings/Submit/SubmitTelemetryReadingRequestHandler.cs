using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.TelemetryReadings.Submit;

internal sealed class SubmitTelemetryReadingRequestHandler(
    ITelemetryIngestionService ingestionService,
    ITenantContext tenantContext)
    : IRequestHandler<SubmitTelemetryReadingRequest, TelemetryReadingResponse>
{
    public Task<TelemetryReadingResponse> Handle(SubmitTelemetryReadingRequest request, CancellationToken cancellationToken)
    {
        // Contract and validation behavior are unchanged; the shared
        // ingestion service owns tag lookup, typed-value validation and
        // persistence so the simulator poller reuses the exact same rules.
        return ingestionService.IngestAsync(
            request.TagId,
            request.ReadAt,
            request.DoubleValue,
            request.StringValue,
            request.Quality,
            tenantContext.TenantId,
            cancellationToken);
    }
}
