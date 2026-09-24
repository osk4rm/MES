using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Create;

public class CreateMaintenanceWorkOrderValidator : RequestValidator<CreateMaintenanceWorkOrderRequest>
{
    public CreateMaintenanceWorkOrderValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required")
            .MaximumLength(50).WithMessage("Code cannot exceed 50 characters");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters");

        RuleFor(x => x.MachineId)
            .NotEmpty().WithMessage("MachineId is required");

        RuleFor(x => x.Priority)
            .Must(Enum.IsDefined).WithMessage("Priority is invalid");
    }
}
