using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.TelemetryReadings;

public record TelemetryReadingResponse(
    Guid Id,
    Guid TagId,
    Guid MachineId,
    DateTime ReadAt,
    double? DoubleValue,
    string? StringValue,
    TelemetryQuality Quality);
