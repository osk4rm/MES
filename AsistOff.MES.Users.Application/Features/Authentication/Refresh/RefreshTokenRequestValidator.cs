using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Users.Application.Features.Authentication.Refresh;

public class RefreshTokenRequestValidator : RequestValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        // The token may be supplied via the httpOnly refresh cookie instead
        // of the body (the controller substitutes it before dispatch), so a
        // missing body token is not a validation failure — the handler
        // rejects it with AuthenticationException (401). An explicitly
        // blank body token is still a malformed request.
        When(x => x.RefreshToken is not null, () =>
        {
            RuleFor(x => x.RefreshToken!)
                .NotEmpty()
                .WithMessage("Refresh token must not be empty when provided");
        });
    }
}
