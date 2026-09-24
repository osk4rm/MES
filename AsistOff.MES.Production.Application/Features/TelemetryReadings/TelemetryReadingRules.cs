using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Exceptions;

namespace AsistOff.MES.Production.Application.Features.TelemetryReadings;

/// <summary>
/// Enforces the "exactly one value, consistent with the tag data type" rule.
/// </summary>
internal static class TelemetryReadingRules
{
    public const int MaxStringValueLength = 256;

    public static void ValidateValue(TelemetryDataType dataType, double? doubleValue, string? stringValue)
    {
        if (dataType == TelemetryDataType.String)
        {
            if (doubleValue.HasValue)
                throw new ValidationException(nameof(doubleValue), "String tags accept a text value only.");
            if (stringValue is null)
                throw new ValidationException(nameof(stringValue), "String value is required for String tags.");
            if (stringValue.Length > MaxStringValueLength)
                throw new ValidationException(nameof(stringValue), $"String value must be at most {MaxStringValueLength} characters.");
            return;
        }

        if (!Enum.IsDefined(dataType))
            throw new ValidationException(nameof(dataType), "Data type is not supported.");
        if (!doubleValue.HasValue)
            throw new ValidationException(nameof(doubleValue), $"Numeric value is required for {dataType} tags.");
        if (stringValue is not null)
            throw new ValidationException(nameof(stringValue), $"{dataType} tags accept a numeric value only.");
    }
}
