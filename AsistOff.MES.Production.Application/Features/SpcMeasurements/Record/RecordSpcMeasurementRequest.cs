using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.SpcMeasurements.Record;

/// <summary>
/// Captures a single measured value against an active SPC characteristic.
/// </summary>
[RequirePermission(RbacDefaults.ProductionWrite)]
public record RecordSpcMeasurementRequest(
    Guid CharacteristicId,
    decimal Value,
    DateTime MeasuredAt,
    string? Notes) : ITenantRequest<SpcMeasurementResponse>;
