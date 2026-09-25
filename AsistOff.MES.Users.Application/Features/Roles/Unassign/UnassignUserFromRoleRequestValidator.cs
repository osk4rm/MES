using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Users.Application.Features.Roles.Unassign;

public sealed class UnassignUserFromRoleRequestValidator : RequestValidator<UnassignUserFromRoleRequest>
{
    public UnassignUserFromRoleRequestValidator()
    {
        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("RoleId is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required");
    }
}
