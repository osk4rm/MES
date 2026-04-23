using AsistOff.MES.Multitenancy.Repositories;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Repositories;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Users.Application.Features.Authentication.SignIn;

public class SignInRequestHandler : IRequestHandler<SignInRequest, JsonWebToken>
{
    private readonly IUsersRepository _usersRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IPasswordHasher<User> _hasher;
    private readonly IAuthManager _authManager;
    private readonly ILogger<SignInRequestHandler> _logger;

    public SignInRequestHandler(
        IUsersRepository usersRepository,
        ITenantRepository tenantRepository,
        IPasswordHasher<User> hasher,
        IAuthManager authManager,
        ILogger<SignInRequestHandler> logger)
    {
        _usersRepository = usersRepository;
        _tenantRepository = tenantRepository;
        _hasher = hasher;
        _authManager = authManager;
        _logger = logger;
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

        var permissions = ResolvePermissions(user);

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

        var token = _authManager.CreateToken(
            userId: user.Id.ToString(),
            role: user.IsTenantAdmin ? "tenant_admin" : "user",
            audience: "AsistOff.MES.Users",
            claims: claims
        );

        return token;
    }

    /// <summary>
    /// Temporary permission resolution. Until a proper RBAC model (Roles / Permissions tables)
    /// is introduced, permissions are derived from the <see cref="User.IsTenantAdmin"/> flag.
    /// </summary>
    private static IEnumerable<string> ResolvePermissions(User user) =>
        user.IsTenantAdmin
            ? new[]
            {
                "users", "users.read", "users.write",
                "configuration", "configuration.read", "configuration.write",
                "tenant.admin"
            }
            : new[]
            {
                "users.read",
                "configuration.read"
            };
}

