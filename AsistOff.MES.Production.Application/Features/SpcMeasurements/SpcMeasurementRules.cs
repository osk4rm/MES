namespace AsistOff.MES.Production.Application.Features.SpcMeasurements;

/// <summary>
/// Western Electric rules 1-4 evaluated against control limits:
/// rule 1 (single point beyond LCL/UCL), rule 2 (2 of 3 consecutive points
/// beyond 2-sigma on the same side), rule 3 (4 of 5 beyond 1-sigma on the
/// same side), rule 4 (8 consecutive points on one side of the centre line).
/// Sigma zones are derived from the control limits around the centre
/// (the nominal value when present, otherwise the mid of the control
/// limits). When control limits are absent no point is out of control and
/// no rule is violated. Spec evaluation is a separate range check.
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

    /// <summary>
    /// Sigma zones around the centre line. Thresholds are per-side so that
    /// three sigma always meets the control limit on that side, even when
    /// the nominal value is off-centre. A point exactly on a threshold is
    /// not beyond it; a point exactly on the centre is on neither side.
    /// </summary>
    internal sealed record SpcControlZones(
        decimal Centre,
        decimal UpperOneSigma,
        decimal UpperTwoSigma,
        decimal LowerOneSigma,
        decimal LowerTwoSigma);

    /// <summary>
    /// Resolves the sigma zones, or <c>null</c> when zones cannot be derived
    /// (either control limit missing, or limits not ordered). A nominal
    /// value outside the control limits falls back to the mid of the limits
    /// so zones never invert.
    /// </summary>
    internal static SpcControlZones? TryResolveZones(
        decimal? nominalValue, decimal? lowerControlLimit, decimal? upperControlLimit)
    {
        if (!lowerControlLimit.HasValue || !upperControlLimit.HasValue)
            return null;

        var lcl = lowerControlLimit.Value;
        var ucl = upperControlLimit.Value;
        if (lcl >= ucl)
            return null;

        var centre = nominalValue.HasValue && nominalValue.Value > lcl && nominalValue.Value < ucl
            ? nominalValue.Value
            : (lcl + ucl) / 2;

        var sigmaUpper = (ucl - centre) / 3;
        var sigmaLower = (centre - lcl) / 3;

        return new SpcControlZones(
            centre,
            centre + sigmaUpper,
            centre + 2 * sigmaUpper,
            centre - sigmaLower,
            centre - 2 * sigmaLower);
    }

    /// <summary>
    /// Evaluates Western Electric rules 1-4 for one ordered window of values.
    /// The caller must pass values in <c>MeasuredAt</c> ascending order; only
    /// the given window is considered (no cross-characteristic leakage).
    /// Returns one violated-rule list per value, in the same order. Rule 1
    /// flags the point itself; rules 2-4 flag the point completing each
    /// violating window, so overlapping windows flag each completing point.
    /// </summary>
    internal static IReadOnlyList<IReadOnlyCollection<int>> EvaluateRules(
        IReadOnlyList<decimal> values,
        decimal? nominalValue,
        decimal? lowerControlLimit,
        decimal? upperControlLimit)
    {
        var violated = Enumerable.Range(0, values.Count)
            .Select(_ => (ICollection<int>)new List<int>())
            .ToList();

        for (var i = 0; i < values.Count; i++)
        {
            if (IsOutOfControl(values[i], lowerControlLimit, upperControlLimit))
                violated[i].Add(1);
        }

        var zones = TryResolveZones(nominalValue, lowerControlLimit, upperControlLimit);
        if (zones is null)
            return violated.Select(v => (IReadOnlyCollection<int>)v.ToList()).ToList();

        for (var i = 0; i < values.Count; i++)
        {
            // Rule 2: 2 of the last 3 points beyond 2-sigma on the same side.
            if (i >= 2 && IsTwoOfThreeBeyond(values, i, zones.UpperTwoSigma, zones.LowerTwoSigma))
                AddRule(violated, i, 2);

            // Rule 3: 4 of the last 5 points beyond 1-sigma on the same side.
            if (i >= 4 && IsFourOfFiveBeyond(values, i, zones.UpperOneSigma, zones.LowerOneSigma))
                AddRule(violated, i, 3);

            // Rule 4: the last 8 points strictly on one side of the centre.
            if (i >= 7 && IsEightOnOneSide(values, i, zones.Centre))
                AddRule(violated, i, 4);
        }

        return violated.Select(v => (IReadOnlyCollection<int>)v.ToList()).ToList();
    }

    private static void AddRule(List<ICollection<int>> violated, int index, int rule)
    {
        if (!violated[index].Contains(rule))
            violated[index].Add(rule);
    }

    private static bool IsTwoOfThreeBeyond(
        IReadOnlyList<decimal> values, int endIndex, decimal upperThreshold, decimal lowerThreshold)
    {
        var above = 0;
        var below = 0;
        for (var i = endIndex - 2; i <= endIndex; i++)
        {
            if (values[i] > upperThreshold)
                above++;
            else if (values[i] < lowerThreshold)
                below++;
        }

        return above >= 2 || below >= 2;
    }

    private static bool IsFourOfFiveBeyond(
        IReadOnlyList<decimal> values, int endIndex, decimal upperThreshold, decimal lowerThreshold)
    {
        var above = 0;
        var below = 0;
        for (var i = endIndex - 4; i <= endIndex; i++)
        {
            if (values[i] > upperThreshold)
                above++;
            else if (values[i] < lowerThreshold)
                below++;
        }

        return above >= 4 || below >= 4;
    }

    private static bool IsEightOnOneSide(IReadOnlyList<decimal> values, int endIndex, decimal centre)
    {
        var above = true;
        var below = true;
        for (var i = endIndex - 7; i <= endIndex; i++)
        {
            if (values[i] > centre)
                below = false;
            else if (values[i] < centre)
                above = false;
            else
                return false;
        }

        return above || below;
    }
}
