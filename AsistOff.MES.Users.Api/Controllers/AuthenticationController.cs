using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Infrastructure.Auth;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using AsistOff.MES.Users.Api.Filters;
using AsistOff.MES.Users.Application.Features.Authentication.Csrf;
using AsistOff.MES.Users.Application.Features.Authentication.Refresh;
using AsistOff.MES.Users.Application.Features.Authentication.SignIn;
using AsistOff.MES.Users.Application.Features.Authentication.SignOut;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Users.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthenticationController(ISender sender, AuthOptions authOptions) : ApiController
{
    /// <summary>
    /// Issues a double-submit CSRF token (issue #376). Anonymous by design:
    /// the token carries no authority by itself. Plants the signed token in
    /// the readable <c>mes_csrf</c> cookie and returns the same value in the
    /// body; the caller echoes it back on every auth write. Cookie writes
    /// must be same-host (SameSite=Lax baseline plus the Origin check).
    /// Requests without an <c>Origin</c> header (non-browser clients) pass
    /// through and receive a token like any browser caller.
    /// </summary>
    [HttpGet("csrf")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCsrfToken(CancellationToken cancellationToken)
    {
        if (!AuthCookies.IsOriginAllowed(Request))
        {
            throw new ForbiddenException("Cross-origin request rejected.");
        }

        var result = await sender.Send(new GetCsrfTokenRequest(), cancellationToken);

        CsrfTokens.AppendCsrfCookie(Response, result.CsrfToken);

        return Ok(result);
    }

    /// <summary>
    /// Cookie-transport sign-in (issue #241, slice 1 of 2). Issues httpOnly
    /// Secure SameSite=Lax cookies for the access + refresh tokens; the
    /// response body no longer carries usable token strings for storage.
    /// CSRF-gated (issue #376): the echoed token must match the
    /// <c>mes_csrf</c> cookie, else 403 and no session is issued.
    /// </summary>
    [HttpPost("sign-in")]
    [AllowAnonymous]
    [RequireCsrfToken]
    public async Task<IActionResult> Login([FromBody] SignInRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);

        AuthCookies.AppendAuthCookies(Response, result.AccessToken, result.RefreshToken, authOptions);

        // The cookies are the transport now: clear the body copies so XSS or
        // careless logging can never lift a usable token from the payload.
        result.AccessToken = string.Empty;
        result.RefreshToken = string.Empty;

        return Ok(result);
    }

    /// <summary>
    /// Rotates a single-use opaque refresh token. Anonymous by design: the caller
    /// presents only the refresh token (their access token may already be expired).
    /// The token is read from the request body when supplied, otherwise from the
    /// httpOnly refresh cookie. Tenant binding comes from the stored refresh-token
    /// row, never from caller input. CSRF-gated (issue #376): a missing or
    /// mismatched token is rejected with 403 and rotates nothing.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [RequireCsrfToken]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest? request, CancellationToken cancellationToken)
    {
        var rawToken = request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(rawToken)
            && Request.Cookies.TryGetValue(AuthCookies.RefreshCookieName, out var cookieToken))
        {
            rawToken = cookieToken;
        }

        var result = await sender.Send(new RefreshTokenRequest(rawToken), cancellationToken);

        AuthCookies.AppendAuthCookies(Response, result.AccessToken, result.RefreshToken, authOptions);

        result.AccessToken = string.Empty;
        result.RefreshToken = string.Empty;

        return Ok(result);
    }

    /// <summary>
    /// Revokes refresh tokens for the current user and clears both auth
    /// cookies. When a refresh token is supplied (body, otherwise the refresh
    /// cookie) only that token is revoked; otherwise all active tokens are revoked.
    /// CSRF-gated (issue #376): a missing or mismatched token is rejected
    /// with 403 and clears nothing.
    /// </summary>
    [HttpPost("sign-out")]
    [Authorize]
    [RequireCsrfToken]
    public async Task<IActionResult> SignOut([FromBody] SignOutRequest? request, CancellationToken cancellationToken)
    {
        var rawToken = request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(rawToken)
            && Request.Cookies.TryGetValue(AuthCookies.RefreshCookieName, out var cookieToken))
        {
            rawToken = cookieToken;
        }

        await sender.Send(new SignOutRequest(rawToken), cancellationToken);

        AuthCookies.ClearAuthCookies(Response);

        return NoContent();
    }
}
