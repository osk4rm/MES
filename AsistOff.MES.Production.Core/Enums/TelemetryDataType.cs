namespace AsistOff.MES.Production.Domain.Enums;

/// <summary>
/// CLR data type of a <see cref="Entities.MachineTelemetryTag"/> value.
/// Numeric and boolean values travel in <c>TelemetryReading.DoubleValue</c>;
/// text values travel in <c>TelemetryReading.StringValue</c>.
/// </summary>
public enum TelemetryDataType : short
{
    Boolean = 1,
    Double = 2,
    Integer = 3,
    String = 4
}
