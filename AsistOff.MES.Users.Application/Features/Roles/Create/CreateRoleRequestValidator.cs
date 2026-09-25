using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Users.Application.Features.Roles.Create;

public sealed class CreateRoleRequestValidator : RequestValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required")
            .MaximumLength(64).WithMessage("Code cannot exceed 64 characters")
            .Matches(@"^[a-z0-9_.\-]+$").WithMessage("Code may contain only lowercase letters, digits, '_', '.' and '-'");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(200).WithMessage("Name cannot exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters")
            .When(x => x.Description is not null);
    }
}
