using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Users.Application.Features.Roles.SetPermissions;

public sealed class SetRolePermissionsRequestValidator : RequestValidator<SetRolePermissionsRequest>
{
    public SetRolePermissionsRequestValidator()
    {
        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("RoleId is required");

        RuleFor(x => x.PermissionIds)
            .NotNull().WithMessage("PermissionIds are required");

        RuleForEach(x => x.PermissionIds)
            .NotEmpty().WithMessage("Permission ids must not be empty");
    }
}
