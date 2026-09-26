using AsistOff.MES.Configuration.Domain.Entities;

namespace AsistOff.MES.Configuration.Application.Features.MaintenancePlans;

/// <summary>
/// Due-badge derivation for preventive maintenance plans (slice 3/3).
/// A plan is overdue when its <c>NextDueAt</c> has passed; <c>DueInDays</c> is
/// the whole-day distance from now to <c>NextDueAt</c> (negative when overdue,
/// null when the plan has no time schedule). Meter-only plans without a
/// <c>NextDueAt</c> are never overdue.
/// </summary>
internal static class MaintenancePlanDueBadge
{
    public static bool IsOverdue(MaintenancePlan plan, DateTime nowUtc)
        => plan.IsActive && plan.NextDueAt.HasValue && plan.NextDueAt.Value <= nowUtc;

    public static int? DueInDays(MaintenancePlan plan, DateTime nowUtc)
    {
        if (!plan.NextDueAt.HasValue)
            return null;

        return (int)Math.Floor((plan.NextDueAt.Value - nowUtc).TotalDays);
    }
}
