using AsistOff.MES.Configuration.Domain.Enums;

namespace AsistOff.MES.Configuration.Application.Features.MeasureUnits.Responses;

public record MeasureUnitResponse(
    Guid Id,
    string Name,
    string Symbol,
    MeasureUnitType Type,
    decimal? ConversionFactor,
    Guid? BaseUnitId,
    string? BaseUnitName,
    bool IsActive,
    string? Description,
    string? SyncId
);
