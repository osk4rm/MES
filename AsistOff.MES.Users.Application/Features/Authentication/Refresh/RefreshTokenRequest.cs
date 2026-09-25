using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;

namespace AsistOff.MES.Users.Application.Features.Authentication.Refresh;

/// <summary>
/// Rotates a single-use opaque refresh token. Authenticated tenant required;
/// the token is resolved through the ambient tenant and the caller's user id,
/// never by a caller-supplied tenant id.
/// </summary>
public record RefreshTokenRequest(string RefreshToken) : ITenantRequest<JsonWebToken>;
