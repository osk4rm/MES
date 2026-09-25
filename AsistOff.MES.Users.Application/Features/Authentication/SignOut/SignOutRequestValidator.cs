using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Users.Application.Features.Authentication.SignOut;

public class SignOutRequestValidator : RequestValidator<SignOutRequest>
{
    public SignOutRequestValidator()
    {
        // RefreshToken is optional (null/empty revokes all); when supplied it must be non-blank.
        When(x => x.RefreshToken is not null, () =>
        {
            RuleFor(x => x.RefreshToken!)
                .NotEmpty()
                .WithMessage("Refresh token must not be empty when provided");
        });
    }
}
