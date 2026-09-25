using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Infrastructure.Auth;
using AsistOff.MES.Shared.Infrastructure.Controllers;
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
    /// Cookie-transport sign-in (issue #241, slice 1 of 2). Issues httpOnly
    /// Secure SameSite=Lax cookies for the access + refresh tokens; the
    /// response body no longer carries usable token strings for storage.
    /// </summary>
    [HttpPost("sign-in")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] SignInRequest request, CancellationToken cancellationToken)
    {
        RejectCrossOriginWrites();

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
    /// row, never from caller input.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest? request, CancellationToken cancellationToken)
    {
        RejectCrossOriginWrites();

        var rawToken = request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(rawToken)
            && Request.Cookies.TryGetValue(AuthCookies.RefreshCookieName, out var cookieToken))
        {
            rawToken = cookieToken;
        }

        var result = await sender.Send(new RefreshTokenRequest(rawToken ?? string.Empty), cancellationToken);

        AuthCookies.AppendAuthCookies(Response, result.AccessToken, result.RefreshToken, authOptions);

        result.AccessToken = string.Empty;
        result.RefreshToken = string.Empty;

        return Ok(result);
    }

    /// <summary>
    /// Revokes refresh tokens for the current user and clears both auth
    /// cookies. When a refresh token is supplied (body, otherwise the refresh
    /// cookie) only that token is revoked; otherwise all active tokens are revoked.
    /// </summary>
    [HttpPost("sign-out")]
    [Authorize]
    public async Task<IActionResult> SignOut([FromBody] SignOutRequest? request, CancellationToken cancellationToken)
    {
        RejectCrossOriginWrites();

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

    /// <summary>
    /// CSRF second layer next to SameSite=Lax: cookie writes must be
    /// same-host. Non-browser callers send no Origin header and pass through.
    /// </summary>
    private void RejectCrossOriginWrites()
    {
        if (!AuthCookies.IsOriginAllowed(Request))
        {
            throw new ForbiddenException("Cross-origin request rejected.");
        }
    }
}
