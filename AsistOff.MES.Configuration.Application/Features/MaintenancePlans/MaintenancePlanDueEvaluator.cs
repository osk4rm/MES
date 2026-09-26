using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;

namespace AsistOff.MES.Configuration.Application.Features.MaintenancePlans;

/// <summary>
/// Pure due-selection rules for preventive maintenance plans (slice 2/3).
/// Time plans are due when <c>NextDueAt</c> has passed; Meter plans are due
/// when the supplied current meter reading reaches the plan threshold.
/// Inactive plans are never due. No OPC UA wiring — the reading arrives as a
/// request parameter.
/// </summary>
internal static class MaintenancePlanDueEvaluator
{
    public static bool IsDue(MaintenancePlan plan, DateTime nowUtc, decimal? meterReading)
    {
        if (!plan.IsActive)
            return false;

        return plan.TriggerType switch
        {
            MaintenancePlanTriggerType.Time =>
                plan.NextDueAt.HasValue && plan.NextDueAt.Value <= nowUtc,
            MaintenancePlanTriggerType.Meter =>
                meterReading.HasValue
                && plan.MeterIntervalValue.HasValue
                && meterReading.Value >= plan.MeterIntervalValue.Value,
            _ => false,
        };
    }

    public static decimal? ResolveMeterReading(
        MaintenancePlan plan,
        decimal? currentMeterReading,
        IReadOnlyDictionary<Guid, decimal>? meterReadings)
    {
        if (meterReadings is not null && meterReadings.TryGetValue(plan.MachineId, out var perMachine))
            return perMachine;

        return currentMeterReading;
    }
}
