using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.SpcMeasurements.Record;

/// <summary>
/// Captures a single measured value against an active SPC characteristic.
/// </summary>
public record RecordSpcMeasurementRequest(
    Guid CharacteristicId,
    decimal Value,
    DateTime MeasuredAt,
    string? Notes) : ITenantRequest<SpcMeasurementResponse>;
