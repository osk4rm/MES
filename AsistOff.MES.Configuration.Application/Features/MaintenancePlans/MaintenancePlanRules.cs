using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Exceptions;

namespace AsistOff.MES.Configuration.Application.Features.MaintenancePlans;

/// <summary>Shared trigger-coherence rules for preventive maintenance plans.</summary>
internal static class MaintenancePlanRules
{
    public static void ValidateSchedule(
        MaintenancePlanTriggerType triggerType,
        int? intervalDays,
        decimal? meterIntervalValue,
        DateTime? nextDueAt,
        DateTime nowUtc)
    {
        if (!Enum.IsDefined(triggerType))
            throw new ValidationException(nameof(triggerType), "TriggerType is invalid.");

        if (triggerType == MaintenancePlanTriggerType.Time)
        {
            if (!intervalDays.HasValue || intervalDays.Value <= 0)
                throw new ValidationException(nameof(intervalDays), "IntervalDays must be greater than zero for Time plans.");
            if (!nextDueAt.HasValue)
                throw new ValidationException(nameof(nextDueAt), "NextDueAt is required for Time plans.");
            if (nextDueAt.Value < nowUtc)
                throw new ValidationException(nameof(nextDueAt), "NextDueAt cannot be in the past.");
        }
        else
        {
            if (!meterIntervalValue.HasValue || meterIntervalValue.Value <= 0)
                throw new ValidationException(nameof(meterIntervalValue), "MeterIntervalValue must be greater than zero for Meter plans.");
            if (nextDueAt.HasValue && nextDueAt.Value < nowUtc)
                throw new ValidationException(nameof(nextDueAt), "NextDueAt cannot be in the past.");
        }
    }
}
