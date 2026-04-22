using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Multitenancy.Requests.Commands.Create;

public class CreateTenantCommandValidator : RequestValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password must not be empty")
            .DependentRules(() =>
            {
                RuleFor(x => x.ConfirmPassword)
                    .Equal(x => x.Password)
                    .WithMessage("Passwords do not match");
            });
    }
}