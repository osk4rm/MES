using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Users.Application.Features.Authentication.Refresh;

public class RefreshTokenRequestValidator : RequestValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        // RefreshToken is optional at validation: the controller merges the
        // refresh cookie when the body carries none. When supplied it must be
        // non-blank; a fully absent token is rejected by the handler with 401
        // (not 400) so anonymous callers get authentication semantics.
        When(x => x.RefreshToken is not null, () =>
        {
            RuleFor(x => x.RefreshToken!)
                .NotEmpty()
                .WithMessage("Refresh token must not be empty when provided");
        });
    }
}
