using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Configuration.Application.Features.MaintenancePlans.EvaluateDue;

public class EvaluateDueMaintenancePlansRequestValidator
    : RequestValidator<EvaluateDueMaintenancePlansRequest>
{
    public EvaluateDueMaintenancePlansRequestValidator()
    {
        When(x => x.CurrentMeterReading.HasValue, () =>
        {
            RuleFor(x => x.CurrentMeterReading!.Value)
                .GreaterThanOrEqualTo(0).WithMessage("CurrentMeterReading cannot be negative");
        });

        When(x => x.MeterReading.HasValue, () =>
        {
            RuleFor(x => x.MeterReading!.Value)
                .GreaterThanOrEqualTo(0).WithMessage("MeterReading cannot be negative");
        });

        When(x => x.MeterReadings != null, () =>
        {
            RuleForEach(x => x.MeterReadings!.Values)
                .GreaterThanOrEqualTo(0).WithMessage("Meter readings cannot be negative");
        });
    }
}
