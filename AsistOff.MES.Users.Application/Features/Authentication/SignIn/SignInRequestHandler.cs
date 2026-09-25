using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Multitenancy.Repositories;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Shared.Infrastructure.Auth;
using AsistOff.MES.Users.Application.Features.Authentication.Refresh;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Rbac;
using AsistOff.MES.Users.Core.Repositories;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Users.Application.Features.Authentication.SignIn;

public class SignInRequestHandler : IRequestHandler<SignInRequest, JsonWebToken>
{
    private readonly IUsersRepository _usersRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserRolesRepository _userRolesRepository;
    private readonly IRolePermissionsRepository _rolePermissionsRepository;
    private readonly IPasswordHasher<User> _hasher;
    private readonly IAuthManager _authManager;
    private readonly ILogger<SignInRequestHandler> _logger;
    private readonly IRefreshTokensRepository? _refreshTokens;
    private readonly AuthOptions? _authOptions;
    private readonly IDateTimeProvider? _dateTimeProvider;
    private readonly IGuidProvider? _guidProvider;

    public SignInRequestHandler(
        IUsersRepository usersRepository,
        ITenantRepository tenantRepository,
        IUserRolesRepository userRolesRepository,
        IRolePermissionsRepository rolePermissionsRepository,
        IPasswordHasher<User> hasher,
        IAuthManager authManager,
        ILogger<SignInRequestHandler> logger,
        IRefreshTokensRepository? refreshTokens = null,
        AuthOptions? authOptions = null,
        IDateTimeProvider? dateTimeProvider = null,
        IGuidProvider? guidProvider = null)
    {
        _usersRepository = usersRepository;
        _tenantRepository = tenantRepository;
        _userRolesRepository = userRolesRepository;
        _rolePermissionsRepository = rolePermissionsRepository;
        _hasher = hasher;
        _authManager = authManager;
        _logger = logger;
        _refreshTokens = refreshTokens;
        _authOptions = authOptions;
        _dateTimeProvider = dateTimeProvider;
        _guidProvider = guidProvider;
    }

    public async Task<JsonWebToken> Handle(SignInRequest request, CancellationToken cancellationToken)
    {
        var user = await _usersRepository.GetForAuthenticationAsync(request.Email, cancellationToken);

        if (user is null)
        {
            _logger.LogInformation("Sign-in failed: unknown email {Email}", request.Email);
            throw new AuthenticationException("Invalid credentials");
        }

        var verificationResult = _hasher.VerifyHashedPassword(user, user.Password, request.Password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            _logger.LogInformation("Sign-in failed for user {UserId}", user.Id);
            throw new AuthenticationException("Invalid credentials");
        }

        var tenant = await _tenantRepository.GetByIdAsync(user.TenantId, cancellationToken);
        if (tenant is null)
        {
            _logger.LogWarning("User {UserId} references missing tenant {TenantId}", user.Id, user.TenantId);
            throw new AuthenticationException("Invalid credentials");
        }

        if (!tenant.IsActive)
        {
            _logger.LogInformation("Sign-in rejected: tenant {TenantId} is inactive", tenant.Id);
            throw new AuthenticationException("Tenant is not active. Please contact your administrator.");
        }

        var permissions = await ResolvePermissionsAsync(user, cancellationToken);

        var claims = new Dictionary<string, IEnumerable<string>>
        {
            ["permissions"] = permissions,
            ["tenant_id"] = [user.TenantId.ToString()],
            ["tenant_name"] = [tenant.Name],
            ["tenant_active"] = [tenant.IsActive.ToString().ToLowerInvariant()],
            ["email"] = [user.Email]
        };

        if (!string.IsNullOrWhiteSpace(tenant.DisplayName))
        {
            claims["tenant_display_name"] = [tenant.DisplayName];
        }

        var audience = _authOptions?.Audience ?? "AsistOff.MES.Users";
        var token = _authManager.CreateToken(
            userId: user.Id.ToString(),
            role: user.IsTenantAdmin ? RbacDefaults.AdminRoleCode : RbacDefaults.UserRoleCode,
            audience: audience,
            claims: claims
        );

        await IssueRefreshTokenAsync(user, cancellationToken, token);

        return token;
    }

    private async Task IssueRefreshTokenAsync(User user, CancellationToken cancellationToken, JsonWebToken token)
    {
        // Refresh store is optional in unit-test construction; in production DI
        // it is always registered, so sign-in always returns a refresh token.
        if (_refreshTokens is null)
        {
            return;
        }

        var now = _dateTimeProvider?.UtcNow ?? DateTime.UtcNow;
        var refreshLifetime = _authOptions?.RefreshTokenLifetime ?? TimeSpan.FromDays(7);
        var (refreshToken, refreshHash) = RefreshTokenHasher.Generate();

        var entity = new RefreshToken
        {
            Id = _guidProvider?.NewGuid() ?? Guid.NewGuid(),
            TenantId = user.TenantId,
            UserId = user.Id,
            TokenHash = refreshHash,
            FamilyId = _guidProvider?.NewGuid() ?? Guid.NewGuid(),
            ExpiresAtUtc = now.Add(refreshLifetime),
            CreatedAt = now
        };

        await _refreshTokens.AddAsync(entity, cancellationToken);
        token.RefreshToken = refreshToken;
    }

    /// <summary>
    /// Materialises the permissions claim from the user's role assignments
    /// through the tenant-scoped <c>UserRole → RolePermission</c> links.
    /// Users without an explicit role assignment (e.g. users predating the
    /// RBAC schema) fall back to the seeded parity sets derived from
    /// <see cref="User.IsTenantAdmin"/>, so sign-in behavior is unchanged
    /// for them. The lookup runs inside an explicit tenant scope for the
    /// caller's tenant, so the tenant-filtered repositories (global query
    /// filter, no <c>IgnoreQueryFilters</c>) can never leak another tenant's
    /// role assignments into the claim.
    /// </summary>
    private async Task<IReadOnlyList<string>> ResolvePermissionsAsync(User user, CancellationToken cancellationToken)
    {
        using (BackgroundTenantContext.BeginScope(user.TenantId))
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
    }
}

