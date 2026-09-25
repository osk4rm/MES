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
public class AuthenticationController(ISender sender) : ApiController
{
    [HttpPost("sign-in")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] SignInRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Rotates a single-use opaque refresh token. Requires a valid access token
    /// (authenticated tenant); the refresh token is resolved through the ambient
    /// tenant and the caller's user id.
    /// </summary>
    [HttpPost("refresh")]
    [Authorize]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Revokes refresh tokens for the current user. When a refresh token is
    /// supplied only that token is revoked; otherwise all active tokens are revoked.
    /// </summary>
    [HttpPost("sign-out")]
    [Authorize]
    public async Task<IActionResult> SignOut([FromBody] SignOutRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(request, cancellationToken);

        return NoContent();
    }
}