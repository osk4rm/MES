using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Complete;

public class CompleteMaintenanceWorkOrderValidator : RequestValidator<CompleteMaintenanceWorkOrderRequest>
{
    public CompleteMaintenanceWorkOrderValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required");

        RuleFor(x => x.ResolutionNotes)
            .NotEmpty().WithMessage("Resolution notes are required")
            .MaximumLength(2000).WithMessage("Resolution notes cannot exceed 2000 characters");
    }
}
