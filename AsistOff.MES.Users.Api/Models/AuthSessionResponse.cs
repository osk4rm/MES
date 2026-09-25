namespace AsistOff.MES.Users.Api.Models;

/// <summary>
/// Non-sensitive session metadata returned by the cookie auth endpoints.
/// Tokens travel exclusively via <c>HttpOnly</c> cookies (see
/// <c>AuthCookies</c>); the body deliberately carries no usable token strings
/// so browser JavaScript has nothing to store or leak.
/// </summary>
/// <param name="Expires">Access-token expiry as Unix milliseconds.</param>
/// <param name="UserId">Authenticated user id.</param>
public sealed record AuthSessionResponse(long Expires, string UserId);
