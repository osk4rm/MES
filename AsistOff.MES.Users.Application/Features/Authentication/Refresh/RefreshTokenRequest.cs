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
/// </summary>
/// <param name="RefreshToken">
/// Opaque refresh token. Optional at the MediatR level because the controller
/// merges the refresh cookie when the body carries none; the handler enforces
/// presence with 401 (not 400) so anonymous callers get auth semantics.
/// </param>
/// <param name="AccessToken">
/// Ambient access token (if any) for cross-tenant binding. Populated by the
/// controller from transport — the <c>Authorization</c> header first, then the
/// access cookie — never bound from the request body. When present and its
/// <c>tenant_id</c> differs from the refresh row's tenant, rotation is
/// rejected so a cookie issued under tenant A can never mint a session while
/// the caller presents a tenant B session.
/// </param>
public record RefreshTokenRequest(string? RefreshToken, string? AccessToken = null) : IRequest<JsonWebToken>, IAllowAnonymousRequest;
