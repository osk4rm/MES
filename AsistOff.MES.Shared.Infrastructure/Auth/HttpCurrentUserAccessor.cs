using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AsistOff.MES.Shared.Abstractions.Auth;
using Microsoft.AspNetCore.Http;

namespace AsistOff.MES.Shared.Infrastructure.Auth;

/// <summary>
/// Resolves the current user id from the JWT subject claims
/// (<c>sub</c>, <c>unique_name</c>, <c>NameIdentifier</c>).
/// Returns <c>null</c> outside an authenticated HTTP request.
/// </summary>
internal sealed class HttpCurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public Guid? UserId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user is null)
                return null;

            var sub = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? user.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value
                ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(sub, out var userId) ? userId : null;
        }
    }
}
