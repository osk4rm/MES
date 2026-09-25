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
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly ICurrentUserAccessor _userAccessor;
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
        ICurrentTenantAccessor tenantAccessor,
        ICurrentUserAccessor userAccessor,
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
        _tenantAccessor = tenantAccessor;
        _userAccessor = userAccessor;
        _logger = logger;
    }

    public async Task<JsonWebToken> Handle(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        if (!_tenantAccessor.TryGetTenantId(out var tenantId) || tenantId == Guid.Empty)
        {
            throw new AuthenticationException("No valid tenant found for this request");
        }

        var currentUserId = _userAccessor.UserId;
        if (currentUserId is null || currentUserId == Guid.Empty)
        {
            throw new AuthenticationException("Invalid refresh token");
        }

        var now = _dateTimeProvider.UtcNow;
        var hash = RefreshTokenHasher.Hash(request.RefreshToken);
        var stored = await _refreshTokens.GetByHashAsync(hash, cancellationToken);

        if (stored is null || stored.UserId != currentUserId.Value)
        {
            _logger.LogInformation("Refresh rejected: unknown token for user {UserId}", currentUserId);
            throw new AuthenticationException("Invalid refresh token");
        }

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
            _logger.LogInformation("Refresh rejected: token revoked for user {UserId}", currentUserId);
            throw new AuthenticationException("Invalid refresh token");
        }

        if (stored.IsExpired(now))
        {
            _logger.LogInformation("Refresh rejected: token expired for user {UserId}", currentUserId);
            throw new AuthenticationException("Refresh token has expired");
        }

        var user = await _usersRepository.GetAsync(stored.UserId);
        if (user is null)
        {
            throw new AuthenticationException("Invalid refresh token");
        }

        var tenant = await _tenantRepository.GetByIdAsync(user.TenantId, cancellationToken);
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
            TenantId = user.TenantId,
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
            role: user.IsTenantAdmin ? "tenant_admin" : "user",
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
