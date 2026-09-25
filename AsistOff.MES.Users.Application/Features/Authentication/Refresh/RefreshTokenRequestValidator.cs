using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Users.Application.Features.Authentication.Refresh;

public class RefreshTokenRequestValidator : RequestValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithMessage("Refresh token is required");
    }
}
