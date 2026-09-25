using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Infrastructure.Auth;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using AsistOff.MES.Users.Api.Models;
using AsistOff.MES.Users.Application.Features.Authentication.Refresh;
using AsistOff.MES.Users.Application.Features.Authentication.SignIn;
using AsistOff.MES.Users.Application.Features.Authentication.SignOut;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace AsistOff.MES.Users.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthenticationController(ISender sender, AuthOptions authOptions) : ApiController
{
    /// <summary>
    /// Signs in with credentials. Issues the session over <c>HttpOnly</c>
    /// <c>Secure</c> <c>SameSite=Lax</c> cookies; the body carries only
    /// non-sensitive session metadata, never token strings.
    /// </summary>
    [HttpPost("sign-in")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] SignInRequest request, CancellationToken cancellationToken)
    {
        RequireSameOrigin();

        var result = await sender.Send(request, cancellationToken);
        AuthCookies.AppendAuthCookies(Response.Cookies, result, authOptions);

        return Ok(ToSession(result));
    }

    /// <summary>
    /// Rotates a single-use opaque refresh token. Anonymous by design: the caller
    /// presents only the refresh token (their access token may already be expired).
    /// The refresh token comes from the body when supplied, otherwise from the
    /// refresh cookie. Tenant binding comes from the stored refresh-token row,
    /// never from caller input; when the caller additionally presents an access
    /// session (header or cookie) for another tenant, rotation is rejected.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest? request, CancellationToken cancellationToken)
    {
        RequireSameOrigin();

        var refreshToken = request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            Request.Cookies.TryGetValue(AuthCookies.RefreshCookieName, out refreshToken);
        }

        var result = await sender.Send(new RefreshTokenRequest(refreshToken, GetAmbientAccessToken()), cancellationToken);
        AuthCookies.AppendAuthCookies(Response.Cookies, result, authOptions);

        return Ok(ToSession(result));
    }

    /// <summary>
    /// Revokes refresh tokens for the current user and clears both auth cookies.
    /// When a refresh token is supplied (body first, then the refresh cookie)
    /// only that token is revoked; otherwise all active tokens are revoked.
    /// </summary>
    [HttpPost("sign-out")]
    [Authorize]
    public async Task<IActionResult> SignOut([FromBody] SignOutRequest? request, CancellationToken cancellationToken)
    {
        RequireSameOrigin();

        var refreshToken = request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            Request.Cookies.TryGetValue(AuthCookies.RefreshCookieName, out refreshToken);
        }

        await sender.Send(new SignOutRequest(refreshToken), cancellationToken);
        AuthCookies.ClearAuthCookies(Response.Cookies);

        return NoContent();
    }

    private void RequireSameOrigin()
        => AuthCookies.RequireSameOrigin(Request.Headers[HeaderNames.Origin].ToString(), Request.Host.Host);

    private string? GetAmbientAccessToken()
    {
        Request.Cookies.TryGetValue(AuthCookies.AccessCookieName, out var accessCookie);
        return AuthCookies.GetAccessToken(Request.Headers[HeaderNames.Authorization].ToString(), accessCookie);
    }

    private static AuthSessionResponse ToSession(JsonWebToken tokens)
    {
        if (string.IsNullOrWhiteSpace(tokens.AccessToken) || string.IsNullOrWhiteSpace(tokens.RefreshToken))
        {
            throw new AuthenticationException("Authentication did not produce a complete session");
        }

        return new AuthSessionResponse(tokens.Expires, tokens.Id);
    }
}
