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
/// The token may arrive via the JSON body (transition clients) or via the
/// httpOnly refresh cookie (issue #241); the controller resolves the effective
/// value before dispatch, so this request carries a null/empty token only when
/// neither transport supplied one — which the handler rejects with 401.
/// </summary>
public record RefreshTokenRequest(string? RefreshToken) : IRequest<JsonWebToken>, IAllowAnonymousRequest;
