using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Users.Application.Features.Authentication.SignOut;

/// <summary>
/// Revokes refresh tokens for the current user. Authenticated tenant required;
/// tokens are resolved through the ambient tenant and the caller's user id,
/// never by a caller-supplied tenant id. When <c>RefreshToken</c> is provided
/// only that token is revoked; otherwise all active tokens for the user are revoked.
/// </summary>
public record SignOutRequest(string? RefreshToken) : ITenantRequest;
