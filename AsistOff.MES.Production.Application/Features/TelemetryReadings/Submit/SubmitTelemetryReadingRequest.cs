using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.TelemetryReadings.Submit;

/// <summary>
/// Manual ingest entry point for telemetry readings. The scheduled poller
/// (a later increment) reuses this same request to append polled values.
/// </summary>
public record SubmitTelemetryReadingRequest(
    Guid TagId,
    DateTime ReadAt,
    double? DoubleValue,
    string? StringValue,
    TelemetryQuality Quality = TelemetryQuality.Good) : ITenantRequest<TelemetryReadingResponse>;
