using AsistOff.MES.Shared.Infrastructure.Auth;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using AsistOff.MES.Users.Application.Features.Authentication.Refresh;
using AsistOff.MES.Users.Application.Features.Authentication.SignIn;
using AsistOff.MES.Users.Application.Features.Authentication.SignOut;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Configuration;

namespace AsistOff.MES.Users.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthenticationController(
    ISender sender,
    AuthOptions authOptions,
    IConfiguration configuration) : ApiController
{
    [HttpPost("sign-in")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] SignInRequest request, CancellationToken cancellationToken)
    {
        AuthCookies.ValidateOrigin(Request, configuration);

        var result = await sender.Send(request, cancellationToken);

        // Cookie transport (issue #241, slice 1 of 2): the session moves into
        // httpOnly cookies so shopfloor JavaScript can never read tokens. The
        // body is sanitized so it carries no usable token strings for storage;
        // header-based callers keep working during transition because the JWT
        // pipeline still accepts the Authorization header.
        AuthCookies.AppendAuthCookies(Response, result.AccessToken, result.RefreshToken, authOptions, DateTimeOffset.UtcNow);
        result.AccessToken = string.Empty;
        result.RefreshToken = string.Empty;

        return Ok(result);
    }

    /// <summary>
    /// Rotates a single-use opaque refresh token. Anonymous by design: the caller
    /// presents only the refresh token (their access token may already be expired).
    /// Tenant binding comes from the stored refresh-token row, never from caller input.
    /// The token may arrive via the JSON body (transition clients) or via the
    /// httpOnly refresh cookie; the cookie wins when the body is absent so
    /// cookie-only terminals can rotate without JavaScript touching the token.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshTokenRequest? request,
        CancellationToken cancellationToken)
    {
        AuthCookies.ValidateOrigin(Request, configuration);

        var effective = request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(effective)
            && AuthCookies.TryGetRefreshToken(Request, out var cookieToken))
        {
            effective = cookieToken;
        }

        var result = await sender.Send(new RefreshTokenRequest(effective), cancellationToken);

        AuthCookies.AppendAuthCookies(Response, result.AccessToken, result.RefreshToken, authOptions, DateTimeOffset.UtcNow);
        result.AccessToken = string.Empty;
        result.RefreshToken = string.Empty;

        return Ok(result);
    }

    /// <summary>
    /// Revokes refresh tokens for the current user. When a refresh token is
    /// supplied (body or httpOnly cookie) only that token is revoked; otherwise
    /// all active tokens are revoked. Both auth cookies are cleared so the
    /// browser drops the session.
    /// </summary>
    [HttpPost("sign-out")]
    [Authorize]
    public async Task<IActionResult> SignOut(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] SignOutRequest? request,
        CancellationToken cancellationToken)
    {
        AuthCookies.ValidateOrigin(Request, configuration);

        var effective = request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(effective)
            && AuthCookies.TryGetRefreshToken(Request, out var cookieToken))
        {
            effective = cookieToken;
        }

        await sender.Send(new SignOutRequest(effective), cancellationToken);

        AuthCookies.ClearAuthCookies(Response);

        return NoContent();
    }
}
