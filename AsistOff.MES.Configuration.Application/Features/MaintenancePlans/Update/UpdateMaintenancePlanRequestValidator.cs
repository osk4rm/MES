using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Update;

public class UpdateMaintenancePlanRequestValidator : RequestValidator<UpdateMaintenancePlanRequest>
{
    public UpdateMaintenancePlanRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required")
            .MaximumLength(50).WithMessage("Code cannot exceed 50 characters");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(200).WithMessage("Name cannot exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters");

        RuleFor(x => x.MachineId)
            .NotEmpty().WithMessage("MachineId is required");

        RuleFor(x => x.TriggerType)
            .Must(Enum.IsDefined).WithMessage("TriggerType is invalid");

        When(x => x.TriggerType == MaintenancePlanTriggerType.Time, () =>
        {
            RuleFor(x => x.IntervalDays)
                .NotNull().WithMessage("IntervalDays is required for Time plans")
                .GreaterThan(0).WithMessage("IntervalDays must be greater than zero");

            RuleFor(x => x.NextDueAt)
                .NotNull().WithMessage("NextDueAt is required for Time plans");
        });

        When(x => x.TriggerType == MaintenancePlanTriggerType.Meter, () =>
        {
            RuleFor(x => x.MeterIntervalValue)
                .NotNull().WithMessage("MeterIntervalValue is required for Meter plans")
                .GreaterThan(0).WithMessage("MeterIntervalValue must be greater than zero");
        });
    }
}
