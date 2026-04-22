using AsistOff.MES.Configuration.Application.Features.MeasureUnits.Responses;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.MeasureUnits.Update;

public record UpdateMeasureUnitRequest(
    Guid Id,
    string Name,
    string Symbol,
    MeasureUnitType Type,
    decimal? ConversionFactor,
    Guid? BaseUnitId,
    bool IsActive,
    string? Description,
    string? SyncId
) : ITenantRequest<MeasureUnitResponse>;
