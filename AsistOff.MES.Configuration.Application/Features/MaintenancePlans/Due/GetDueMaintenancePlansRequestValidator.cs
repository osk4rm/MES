using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Due;

public class GetDueMaintenancePlansRequestValidator
    : RequestValidator<GetDueMaintenancePlansRequest>
{
    public GetDueMaintenancePlansRequestValidator()
    {
        When(x => x.DueWithinDays.HasValue, () =>
        {
            RuleFor(x => x.DueWithinDays!.Value)
                .GreaterThan(0).WithMessage("DueWithinDays must be greater than zero");
        });
    }
}
