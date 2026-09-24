using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Exceptions;

namespace AsistOff.MES.Production.Application.Features.SpcCharacteristics;

/// <summary>
/// Shared business rules for SPC characteristic definitions.
/// </summary>
internal static class SpcCharacteristicRules
{
    public static void ValidateLimits(
        SpcChartType chartType,
        decimal? nominalValue,
        decimal? lowerSpecLimit,
        decimal? upperSpecLimit,
        decimal? lowerControlLimit,
        decimal? upperControlLimit,
        int sampleSize)
    {
        if (!Enum.IsDefined(chartType))
            throw new ValidationException(nameof(chartType), "Chart type is not a defined value.");

        if (sampleSize < 1)
            throw new ValidationException(nameof(sampleSize), "Sample size must be at least 1.");

        if (lowerSpecLimit.HasValue && upperSpecLimit.HasValue && lowerSpecLimit.Value >= upperSpecLimit.Value)
            throw new ValidationException(nameof(lowerSpecLimit), "Lower spec limit must be below upper spec limit.");

        if (lowerControlLimit.HasValue && upperControlLimit.HasValue && lowerControlLimit.Value >= upperControlLimit.Value)
            throw new ValidationException(nameof(lowerControlLimit), "Lower control limit must be below upper control limit.");

        if (nominalValue.HasValue && lowerSpecLimit.HasValue && upperSpecLimit.HasValue &&
            (nominalValue.Value < lowerSpecLimit.Value || nominalValue.Value > upperSpecLimit.Value))
            throw new ValidationException(nameof(nominalValue), "Nominal value must lie within the spec limits.");
    }
}
