namespace AsistOff.MES.Production.Application.Features.SpcMeasurements;

/// <summary>
/// Western Electric rule 1 only: a point beyond the control limits is
/// out of control. When control limits are absent the point is never
/// out of control. Spec evaluation is a separate range check.
/// </summary>
internal static class SpcMeasurementRules
{
    public static bool IsOutOfControl(decimal value, decimal? lowerControlLimit, decimal? upperControlLimit)
    {
        if (lowerControlLimit.HasValue && value < lowerControlLimit.Value)
            return true;
        if (upperControlLimit.HasValue && value > upperControlLimit.Value)
            return true;
        return false;
    }

    public static bool IsOutOfSpec(decimal value, decimal? lowerSpecLimit, decimal? upperSpecLimit)
    {
        if (lowerSpecLimit.HasValue && value < lowerSpecLimit.Value)
            return true;
        if (upperSpecLimit.HasValue && value > upperSpecLimit.Value)
            return true;
        return false;
    }
}
