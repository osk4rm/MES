using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Multitenancy.Requests.Commands.Create;

public class CreateTenantCommandValidator : RequestValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tenant name must not be empty")
            .MaximumLength(200).WithMessage("Tenant name cannot exceed 200 characters");

        RuleFor(x => x.ContactEmail)
            .NotEmpty().WithMessage("Contact email must not be empty")
            .EmailAddress().WithMessage("A valid contact email address is required");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password must not be empty")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .DependentRules(() =>
            {
                RuleFor(x => x.ConfirmPassword)
                    .Equal(x => x.Password)
                    .WithMessage("Passwords do not match");
            });
    }
}