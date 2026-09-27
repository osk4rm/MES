using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Shared.Infrastructure.Auth;
using MediatR;

namespace AsistOff.MES.Users.Application.Features.Authentication.Csrf;

/// <summary>
/// Issues a double-submit CSRF token for the cookie-writing auth endpoints
/// (issue #376). Deliberately anonymous
/// (<see cref="IAllowAnonymousRequest"/>): no tenant context exists before
/// sign-in and the token carries no authority by itself — it only becomes
/// meaningful when echoed back alongside the session cookies on
/// sign-in/refresh/sign-out, where the controller compares it against the
/// <c>mes_csrf</c> cookie.
/// </summary>
public record GetCsrfTokenRequest : IRequest<CsrfTokenResponse>, IAllowAnonymousRequest;

/// <summary>Issued token; the controller also plants it in the readable CSRF cookie.</summary>
public record CsrfTokenResponse(string CsrfToken);

public class GetCsrfTokenRequestHandler : IRequestHandler<GetCsrfTokenRequest, CsrfTokenResponse>
{
    private readonly AuthOptions _authOptions;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetCsrfTokenRequestHandler(AuthOptions authOptions, IDateTimeProvider dateTimeProvider)
    {
        _authOptions = authOptions;
        _dateTimeProvider = dateTimeProvider;
    }

    public Task<CsrfTokenResponse> Handle(GetCsrfTokenRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_authOptions.IssuerSigningKey))
        {
            throw new InvalidOperationException("Cannot issue CSRF tokens without auth:IssuerSigningKey.");
        }

        var now = new DateTimeOffset(DateTime.SpecifyKind(_dateTimeProvider.UtcNow, DateTimeKind.Utc), TimeSpan.Zero);
        var token = CsrfTokens.Issue(_authOptions.IssuerSigningKey, now: now);

        return Task.FromResult(new CsrfTokenResponse(token));
    }
}
