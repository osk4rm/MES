namespace AsistOff.MES.Production.Domain.Enums;

/// <summary>
/// OPC UA quality code attached to a <see cref="Entities.TelemetryReading"/>.
/// </summary>
public enum TelemetryQuality : short
{
    Good = 1,
    Bad = 2,
    Uncertain = 3
}
