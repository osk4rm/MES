using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Configuration.Application.Features.MaintenancePlans.RaiseNow;

public class RaiseMaintenancePlanNowRequestValidator
    : RequestValidator<RaiseMaintenancePlanNowRequest>
{
    public RaiseMaintenancePlanNowRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required");
    }
}
