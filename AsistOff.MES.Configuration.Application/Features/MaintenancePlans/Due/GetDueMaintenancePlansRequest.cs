using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Due;

/// <summary>
/// Lists active preventive plans with a time schedule, overdue first
/// (<c>NextDueAt</c> ascending, nulls excluded). Optional
/// <c>OverdueOnly</c> keeps only plans whose <c>NextDueAt</c> has passed;
/// optional <c>DueWithinDays</c> keeps plans due within the next N days
/// (overdue plans always match).
/// </summary>
public class GetDueMaintenancePlansRequest
    : ITenantRequest<IReadOnlyCollection<MaintenancePlanResponse>>
{
    public bool? OverdueOnly { get; set; }
    public int? DueWithinDays { get; set; }
}
