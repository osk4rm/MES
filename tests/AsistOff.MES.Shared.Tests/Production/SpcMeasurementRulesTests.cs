using AsistOff.MES.Production.Application.Features.SpcMeasurements;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Production;

public class SpcMeasurementRulesTests
{
    private const decimal Lcl = 70m;
    private const decimal Ucl = 130m;
    private const decimal Nominal = 100m;

    private static IReadOnlyList<decimal> Values(params decimal[] values) => values;

    [Fact]
    public void TryResolveZones_BothLimitsAbsent_ReturnsNull()
    {
        SpcMeasurementRules.TryResolveZones(Nominal, null, null).Should().BeNull();
    }

    [Fact]
    public void TryResolveZones_SingleLimitAbsent_ReturnsNull()
    {
        SpcMeasurementRules.TryResolveZones(Nominal, Lcl, null).Should().BeNull();
        SpcMeasurementRules.TryResolveZones(Nominal, null, Ucl).Should().BeNull();
    }

    [Fact]
    public void TryResolveZones_UnorderedLimits_ReturnsNull()
    {
        SpcMeasurementRules.TryResolveZones(Nominal, 130m, 70m).Should().BeNull();
        SpcMeasurementRules.TryResolveZones(Nominal, 100m, 100m).Should().BeNull();
    }

    [Fact]
    public void TryResolveZones_UsesNominalAsCentre()
    {
        var zones = SpcMeasurementRules.TryResolveZones(Nominal, Lcl, Ucl);

        zones.Should().NotBeNull();
        zones!.Centre.Should().Be(100m);
        zones.UpperOneSigma.Should().Be(110m);
        zones.UpperTwoSigma.Should().Be(120m);
        zones.LowerOneSigma.Should().Be(90m);
        zones.LowerTwoSigma.Should().Be(80m);
    }

    [Fact]
    public void TryResolveZones_NominalAbsent_FallsBackToMidOfLimits()
    {
        var zones = SpcMeasurementRules.TryResolveZones(null, Lcl, Ucl);

        zones.Should().NotBeNull();
        zones!.Centre.Should().Be(100m);
        zones.UpperOneSigma.Should().Be(110m);
        zones.UpperTwoSigma.Should().Be(120m);
        zones.LowerOneSigma.Should().Be(90m);
        zones.LowerTwoSigma.Should().Be(80m);
    }

    [Fact]
    public void TryResolveZones_NominalOutsideLimits_FallsBackToMid()
    {
        var zones = SpcMeasurementRules.TryResolveZones(200m, Lcl, Ucl);

        zones.Should().NotBeNull();
        zones!.Centre.Should().Be(100m);
    }

    [Fact]
    public void TryResolveZones_OffCentreNominal_ZonesStillMeetControlLimits()
    {
        var zones = SpcMeasurementRules.TryResolveZones(115m, Lcl, Ucl);

        zones.Should().NotBeNull();
        zones!.Centre.Should().Be(115m);
        // Three sigma meets the control limit on each side.
        (zones.Centre + 3 * (zones.UpperOneSigma - zones.Centre)).Should().Be(Ucl);
        (zones.Centre - 3 * (zones.Centre - zones.LowerOneSigma)).Should().Be(Lcl);
        zones.UpperOneSigma.Should().Be(120m);
        zones.UpperTwoSigma.Should().Be(125m);
        zones.LowerOneSigma.Should().Be(100m);
        zones.LowerTwoSigma.Should().Be(85m);
    }

    [Fact]
    public void EvaluateRules_Rule1_PointBeyondLimits_FlagsRule1()
    {
        var result = SpcMeasurementRules.EvaluateRules(Values(100m, 131m), Nominal, Lcl, Ucl);

        result.Should().HaveCount(2);
        result[0].Should().BeEmpty();
        result[1].Should().BeEquivalentTo([1]);
    }

    [Fact]
    public void EvaluateRules_Rule2_TwoOfThreeBeyondTwoSigmaUpper_FlagsLastPoint()
    {
        var result = SpcMeasurementRules.EvaluateRules(Values(100m, 121m, 122m), Nominal, Lcl, Ucl);

        result[0].Should().BeEmpty();
        result[1].Should().BeEmpty();
        result[2].Should().BeEquivalentTo([2]);
    }

    [Fact]
    public void EvaluateRules_Rule2_TwoOfThreeBeyondTwoSigmaLower_FlagsLastPoint()
    {
        var result = SpcMeasurementRules.EvaluateRules(Values(100m, 79m, 78m), Nominal, Lcl, Ucl);

        result[2].Should().BeEquivalentTo([2]);
    }

    [Fact]
    public void EvaluateRules_Rule2_OnlyOneBeyondTwoSigma_NoViolation()
    {
        var result = SpcMeasurementRules.EvaluateRules(Values(100m, 121m, 100m), Nominal, Lcl, Ucl);

        result.SelectMany(v => v).Should().BeEmpty();
    }

    [Fact]
    public void EvaluateRules_Rule2_PointExactlyOnThreshold_DoesNotCount()
    {
        // 120 is exactly the 2-sigma line: not beyond, so only one of three qualifies.
        var result = SpcMeasurementRules.EvaluateRules(Values(100m, 120m, 121m), Nominal, Lcl, Ucl);

        result.SelectMany(v => v).Should().BeEmpty();
    }

    [Fact]
    public void EvaluateRules_Rule2_MixedSides_NoViolation()
    {
        var result = SpcMeasurementRules.EvaluateRules(Values(121m, 100m, 79m), Nominal, Lcl, Ucl);

        result.SelectMany(v => v).Should().BeEmpty();
    }

    [Fact]
    public void EvaluateRules_Rule3_FourOfFiveBeyondOneSigma_FlagsLastPoint()
    {
        var result = SpcMeasurementRules.EvaluateRules(
            Values(100m, 111m, 112m, 113m, 114m), Nominal, Lcl, Ucl);

        result.Take(4).SelectMany(v => v).Should().BeEmpty();
        result[4].Should().BeEquivalentTo([3]);
    }

    [Fact]
    public void EvaluateRules_Rule3_OnlyThreeBeyondOneSigma_NoViolation()
    {
        var result = SpcMeasurementRules.EvaluateRules(
            Values(100m, 111m, 112m, 113m, 100m), Nominal, Lcl, Ucl);

        result.SelectMany(v => v).Should().BeEmpty();
    }

    [Fact]
    public void EvaluateRules_Rule3_PointExactlyOnThreshold_DoesNotCount()
    {
        // 110 is exactly the 1-sigma line: not beyond, so only three of five qualify.
        var result = SpcMeasurementRules.EvaluateRules(
            Values(110m, 110m, 112m, 113m, 114m), Nominal, Lcl, Ucl);

        result.SelectMany(v => v).Should().BeEmpty();
    }

    [Fact]
    public void EvaluateRules_Rule4_EightConsecutiveAboveCentre_FlagsLastPointOnly()
    {
        var result = SpcMeasurementRules.EvaluateRules(
            Values(101m, 101m, 101m, 101m, 101m, 101m, 101m, 101m), Nominal, Lcl, Ucl);

        result.Take(7).SelectMany(v => v).Should().BeEmpty();
        result[7].Should().BeEquivalentTo([4]);
    }

    [Fact]
    public void EvaluateRules_Rule4_EightConsecutiveBelowCentre_FlagsLastPoint()
    {
        var result = SpcMeasurementRules.EvaluateRules(
            Values(99m, 99m, 99m, 99m, 99m, 99m, 99m, 99m), Nominal, Lcl, Ucl);

        result[7].Should().BeEquivalentTo([4]);
    }

    [Fact]
    public void EvaluateRules_Rule4_SevenConsecutive_NoViolation()
    {
        var result = SpcMeasurementRules.EvaluateRules(
            Values(101m, 101m, 101m, 101m, 101m, 101m, 101m), Nominal, Lcl, Ucl);

        result.SelectMany(v => v).Should().BeEmpty();
    }

    [Fact]
    public void EvaluateRules_Rule4_CentrePointBreaksRun_NoViolation()
    {
        var result = SpcMeasurementRules.EvaluateRules(
            Values(101m, 101m, 101m, 101m, 101m, 101m, 101m, 100m), Nominal, Lcl, Ucl);

        result.SelectMany(v => v).Should().BeEmpty();
    }

    [Fact]
    public void EvaluateRules_Rule4_NineConsecutive_FlagsLastTwoPoints()
    {
        var result = SpcMeasurementRules.EvaluateRules(
            Values(101m, 101m, 101m, 101m, 101m, 101m, 101m, 101m, 101m), Nominal, Lcl, Ucl);

        result.Take(7).SelectMany(v => v).Should().BeEmpty();
        result[7].Should().BeEquivalentTo([4]);
        result[8].Should().BeEquivalentTo([4]);
    }

    [Fact]
    public void EvaluateRules_AbsentLimits_ReturnsEmptyEvenForExtremeValues()
    {
        var result = SpcMeasurementRules.EvaluateRules(Values(9999m, -9999m), Nominal, null, null);

        result.Should().HaveCount(2);
        result.SelectMany(v => v).Should().BeEmpty();
    }

    [Fact]
    public void EvaluateRules_SingleLimitPresent_Rule1AppliesButRules24DoNot()
    {
        var result = SpcMeasurementRules.EvaluateRules(
            Values(100m, 121m, 122m, 131m), Nominal, null, Ucl);

        result[0].Should().BeEmpty();
        result[1].Should().BeEmpty();
        result[2].Should().BeEmpty();
        result[3].Should().BeEquivalentTo([1]);
    }

    [Fact]
    public void EvaluateRules_PointViolatingSeveralRules_ListsAllOfThem()
    {
        // 125 is within the control limits but beyond 2-sigma, beyond 1-sigma
        // and above the centre, so the 8th point completes rules 2, 3 and 4.
        var result = SpcMeasurementRules.EvaluateRules(
            Values(125m, 125m, 125m, 125m, 125m, 125m, 125m, 125m), Nominal, Lcl, Ucl);

        result[7].Should().BeEquivalentTo([2, 3, 4]);
    }

    [Fact]
    public void EvaluateRules_NominalAbsent_UsesMidAsCentre()
    {
        // Mid of 70/130 is 100: eight points at 101 are above the mid.
        var result = SpcMeasurementRules.EvaluateRules(
            Values(101m, 101m, 101m, 101m, 101m, 101m, 101m, 101m), null, Lcl, Ucl);

        result[7].Should().BeEquivalentTo([4]);
    }
}
