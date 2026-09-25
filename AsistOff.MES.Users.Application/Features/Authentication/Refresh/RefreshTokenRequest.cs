using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using MediatR;

namespace AsistOff.MES.Users.Application.Features.Authentication.Refresh;

/// <summary>
/// Rotates a single-use opaque refresh token. Deliberately anonymous
/// (<see cref="IAllowAnonymousRequest"/>): the caller presents only the opaque
/// refresh token, typically because their access token already expired, so no
/// ambient tenant or user can be required. Tenant binding comes from the stored
/// refresh-token row's <c>TenantId</c> — never from caller input — and every
/// follow-up read/write runs inside that tenant's scope.
/// The token may arrive via the httpOnly refresh cookie instead of the body
/// (see <c>AuthenticationController</c>), so it is nullable here; the handler
/// rejects a missing token with <c>AuthenticationException</c> (401).
/// </summary>
public record RefreshTokenRequest(string? RefreshToken) : IRequest<JsonWebToken>, IAllowAnonymousRequest;
