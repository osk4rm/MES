using System.Text;
using System.Text.Json;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Multitenancy.Repositories;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Shared.Infrastructure.Auth;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Rbac;
using AsistOff.MES.Users.Core.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Users.Application.Features.Authentication.Refresh;

public class RefreshTokenRequestHandler : IRequestHandler<RefreshTokenRequest, JsonWebToken>
{
    private readonly IRefreshTokensRepository _refreshTokens;
    private readonly IUsersRepository _usersRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserRolesRepository _userRolesRepository;
    private readonly IRolePermissionsRepository _rolePermissionsRepository;
    private readonly IAuthManager _authManager;
    private readonly AuthOptions _authOptions;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGuidProvider _guidProvider;
    private readonly ILogger<RefreshTokenRequestHandler> _logger;

    public RefreshTokenRequestHandler(
        IRefreshTokensRepository refreshTokens,
        IUsersRepository usersRepository,
        ITenantRepository tenantRepository,
        IUserRolesRepository userRolesRepository,
        IRolePermissionsRepository rolePermissionsRepository,
        IAuthManager authManager,
        AuthOptions authOptions,
        IDateTimeProvider dateTimeProvider,
        IGuidProvider guidProvider,
        ILogger<RefreshTokenRequestHandler> logger)
    {
        _refreshTokens = refreshTokens;
        _usersRepository = usersRepository;
        _tenantRepository = tenantRepository;
        _userRolesRepository = userRolesRepository;
        _rolePermissionsRepository = rolePermissionsRepository;
        _authManager = authManager;
        _authOptions = authOptions;
        _dateTimeProvider = dateTimeProvider;
        _guidProvider = guidProvider;
        _logger = logger;
    }

    public async Task<JsonWebToken> Handle(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        // Anonymous by design: the caller presents only the opaque refresh token
        // (their access token may already be expired), so no ambient tenant or
        // user is required here. The row is resolved pre-authentication by its
        // unguessable hash; everything below runs inside the stored row's tenant
        // scope, so a token issued under tenant A can only ever mint a tenant A
        // session — never a session for tenant B.
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new AuthenticationException("Invalid refresh token");
        }

        var hash = RefreshTokenHasher.Hash(request.RefreshToken);
        var stored = await _refreshTokens.GetByHashIgnoringQueryFiltersAsync(hash, cancellationToken);

        if (stored is null)
        {
            _logger.LogInformation("Refresh rejected: unknown token");
            throw new AuthenticationException("Invalid refresh token");
        }

        using (BackgroundTenantContext.BeginScope(stored.TenantId))
        {
            return await RotateAsync(stored, request.AccessToken, cancellationToken);
        }
    }

    /// <summary>
    /// Cross-tenant binding for cookie transport: when the caller presents an
    /// ambient access session (header or access cookie) whose <c>tenant_id</c>
    /// differs from the refresh row's tenant, rotation is rejected with 401
    /// before any state changes, so a refresh cookie issued under tenant A can
    /// never mint a session for a tenant B caller. Anonymous callers (no
    /// ambient token, or an unparseable one such as a stale foreign token)
    /// proceed — the opaque refresh token itself is the credential and its
    /// tenant still comes from the stored row, never from caller input.
    /// The ambient token's signature is intentionally not verified here; the
    /// endpoint is anonymous by design and signature enforcement belongs to
    /// the JWT Bearer pipeline on authenticated endpoints.
    /// </summary>
    private void RejectCrossTenantRefresh(RefreshToken stored, string? ambientAccessToken)
    {
        var ambientTenantId = TryReadTenantId(ambientAccessToken);
        if (ambientTenantId.HasValue && ambientTenantId.Value != stored.TenantId)
        {
            _logger.LogWarning(
                "Refresh rejected: ambient tenant {AmbientTenantId} does not match refresh token tenant {StoredTenantId}",
                ambientTenantId.Value, stored.TenantId);
            throw new AuthenticationException("Refresh token does not match the current session");
        }
    }

    /// <summary>
    /// Reads the <c>tenant_id</c> claim from a raw JWT payload without
    /// validating the signature (see <see cref="RejectCrossTenantRefresh"/>).
    /// Returns <c>null</c> for missing, malformed or tenant-less tokens so the
    /// caller is treated as anonymous.
    /// </summary>
    private static Guid? TryReadTenantId(string? accessToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return null;
            }

            var parts = accessToken.Split('.');
            if (parts.Length != 3)
            {
                return null;
            }

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload += new string('=', (4 - payload.Length % 4) % 4);
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));

            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("tenant_id", out var tenantElement))
            {
                return null;
            }

            string? value = tenantElement.ValueKind switch
            {
                JsonValueKind.String => tenantElement.GetString(),
                JsonValueKind.Array => tenantElement.EnumerateArray()
                    .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : e.ToString())
                    .FirstOrDefault(),
                _ => tenantElement.ToString()
            };

            return Guid.TryParse(value, out var tenantId) ? tenantId : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return null;
        }
    }

    private async Task<JsonWebToken> RotateAsync(RefreshToken stored, string? ambientAccessToken, CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.UtcNow;

        RejectCrossTenantRefresh(stored, ambientAccessToken);

        if (stored.IsRevoked && stored.ReplacedByHash is not null)
        {
            // Reuse detected: a rotated token was presented again. Revoke the
            // whole family so a leaked token cannot be replayed.
            _logger.LogWarning("Refresh token reuse detected for family {FamilyId}; revoking family", stored.FamilyId);
            await RevokeFamilyAsync(stored.FamilyId, "reuse-detected", cancellationToken);
            throw new AuthenticationException("Refresh token has already been used");
        }

        if (stored.IsRevoked)
        {
            _logger.LogInformation("Refresh rejected: token revoked");
            throw new AuthenticationException("Invalid refresh token");
        }

        if (stored.IsExpired(now))
        {
            _logger.LogInformation("Refresh rejected: token expired");
            throw new AuthenticationException("Refresh token has expired");
        }

        var user = await _usersRepository.GetAsync(stored.UserId);
        if (user is null || user.TenantId != stored.TenantId)
        {
            _logger.LogInformation("Refresh rejected: user missing or tenant mismatch");
            throw new AuthenticationException("Invalid refresh token");
        }

        var tenant = await _tenantRepository.GetByIdAsync(stored.TenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
        {
            throw new AuthenticationException("Invalid refresh token");
        }

        // Rotate: invalidate the old token and mint a successor in the same family.
        var (nextToken, nextHash) = RefreshTokenHasher.Generate();
        stored.RevokedAtUtc = now;
        stored.ReplacedByHash = nextHash;
        stored.RevocationReason = "rotated";
        await _refreshTokens.UpdateAsync(stored, cancellationToken);

        var successor = new RefreshToken
        {
            Id = _guidProvider.NewGuid(),
            TenantId = stored.TenantId,
            UserId = user.Id,
            TokenHash = nextHash,
            FamilyId = stored.FamilyId,
            ExpiresAtUtc = now.Add(_authOptions.RefreshTokenLifetime),
            CreatedAt = now
        };
        await _refreshTokens.AddAsync(successor, cancellationToken);

        var permissions = await ResolvePermissionsAsync(user, cancellationToken);
        var claims = BuildClaims(user, tenant.Name, tenant.DisplayName, tenant.IsActive, user.Email, permissions);
        var audience = _authOptions.Audience ?? "AsistOff.MES.Users";
        var access = _authManager.CreateToken(
            userId: user.Id.ToString(),
            role: user.IsTenantAdmin ? RbacDefaults.AdminRoleCode : RbacDefaults.UserRoleCode,
            audience: audience,
            claims: claims);

        access.RefreshToken = nextToken;
        return access;
    }

    private async Task RevokeFamilyAsync(Guid familyId, string reason, CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.UtcNow;
        var members = await _refreshTokens.BrowseByFamilyAsync(familyId, cancellationToken);
        foreach (var member in members.Where(m => !m.IsRevoked))
        {
            member.RevokedAtUtc = now;
            member.RevocationReason = reason;
            await _refreshTokens.UpdateAsync(member, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<string>> ResolvePermissionsAsync(User user, CancellationToken cancellationToken)
    {
        var assignments = await _userRolesRepository.BrowseByUserAsync(user.Id, cancellationToken);
        if (assignments.Count == 0)
        {
            return user.IsTenantAdmin ? RbacDefaults.AdminPermissions : RbacDefaults.UserPermissions;
        }

        var permissions = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var assignment in assignments)
        {
            var links = await _rolePermissionsRepository.BrowseByRoleAsync(assignment.RoleId, cancellationToken);
            foreach (var link in links)
            {
                if (!string.IsNullOrWhiteSpace(link.Permission?.Code))
                {
                    permissions.Add(link.Permission.Code);
                }
            }
        }

        return permissions.ToList();
    }

    private static Dictionary<string, IEnumerable<string>> BuildClaims(
        User user, string tenantName, string? tenantDisplayName, bool tenantActive, string email,
        IReadOnlyList<string> permissions)
    {
        var claims = new Dictionary<string, IEnumerable<string>>
        {
            ["permissions"] = permissions,
            ["tenant_id"] = [user.TenantId.ToString()],
            ["tenant_name"] = [tenantName],
            ["tenant_active"] = [tenantActive.ToString().ToLowerInvariant()],
            ["email"] = [email]
        };

        if (!string.IsNullOrWhiteSpace(tenantDisplayName))
        {
            claims["tenant_display_name"] = [tenantDisplayName];
        }

        return claims;
    }
}
