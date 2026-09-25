using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.TelemetryReadings.Submit;

/// <summary>
/// Manual ingest entry point for telemetry readings. The scheduled poller
/// (a later increment) reuses this same request to append polled values.
/// </summary>
[RequirePermission(RbacDefaults.ProductionWrite)]
public record SubmitTelemetryReadingRequest(
    Guid TagId,
    DateTime ReadAt,
    double? DoubleValue,
    string? StringValue,
    TelemetryQuality Quality = TelemetryQuality.Good) : ITenantRequest<TelemetryReadingResponse>;
