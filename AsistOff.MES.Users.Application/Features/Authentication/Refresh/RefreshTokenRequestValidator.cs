using AsistOff.MES.Shared.Abstractions.Validation;

namespace AsistOff.MES.Users.Application.Features.Authentication.Refresh;

public class RefreshTokenRequestValidator : RequestValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        // No NotEmpty rule by design (issue #241): the refresh token may arrive
        // via the httpOnly cookie with an empty body, and a missing token is an
        // authentication failure (401 from the handler), not a validation
        // failure (400). The handler rejects null/whitespace with
        // AuthenticationException.
    }
}
