using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;

namespace AsistOff.MES.Configuration.Application.Features.MaintenancePlans;

/// <summary>
/// Completion rollover for preventive maintenance plans (slice 3/3).
/// Completing a plan-linked work order stamps the plan's
/// <c>LastCompletedAt</c> and rolls a Time plan's <c>NextDueAt</c> forward by
/// <c>IntervalDays</c> from the completion instant. Meter plans keep their
/// schedule — only <c>LastCompletedAt</c> advances.
/// </summary>
internal static class MaintenancePlanRollover
{
    public static void Apply(MaintenancePlan plan, DateTime completedAtUtc)
    {
        plan.LastCompletedAt = completedAtUtc;

        if (plan.TriggerType == MaintenancePlanTriggerType.Time
            && plan.IntervalDays.HasValue
            && plan.IntervalDays.Value > 0)
        {
            plan.NextDueAt = completedAtUtc.AddDays(plan.IntervalDays.Value);
        }
    }
}
