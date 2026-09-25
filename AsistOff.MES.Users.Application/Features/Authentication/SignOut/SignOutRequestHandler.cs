using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Users.Application.Features.Authentication.Refresh;
using AsistOff.MES.Users.Core.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Users.Application.Features.Authentication.SignOut;

public class SignOutRequestHandler : IRequestHandler<SignOutRequest>
{
    private readonly IRefreshTokensRepository _refreshTokens;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly ICurrentUserAccessor _userAccessor;
    private readonly ILogger<SignOutRequestHandler> _logger;

    public SignOutRequestHandler(
        IRefreshTokensRepository refreshTokens,
        IDateTimeProvider dateTimeProvider,
        ICurrentTenantAccessor tenantAccessor,
        ICurrentUserAccessor userAccessor,
        ILogger<SignOutRequestHandler> logger)
    {
        _refreshTokens = refreshTokens;
        _dateTimeProvider = dateTimeProvider;
        _tenantAccessor = tenantAccessor;
        _userAccessor = userAccessor;
        _logger = logger;
    }

    public async Task Handle(SignOutRequest request, CancellationToken cancellationToken)
    {
        if (!_tenantAccessor.TryGetTenantId(out var tenantId) || tenantId == Guid.Empty)
        {
            throw new AuthenticationException("No valid tenant found for this request");
        }

        var currentUserId = _userAccessor.UserId;
        if (currentUserId is null || currentUserId == Guid.Empty)
        {
            throw new AuthenticationException("No valid user found for this request");
        }

        var now = _dateTimeProvider.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var hash = RefreshTokenHasher.Hash(request.RefreshToken);
            var stored = await _refreshTokens.GetByHashAsync(hash, cancellationToken);
            if (stored is null || stored.UserId != currentUserId.Value)
            {
                // Idempotent sign-out: unknown tokens are a no-op so retries stay safe.
                _logger.LogInformation("Sign-out: unknown token for user {UserId}; no-op", currentUserId);
                return;
            }

            if (!stored.IsRevoked)
            {
                stored.RevokedAtUtc = now;
                stored.RevocationReason = "sign-out";
                await _refreshTokens.UpdateAsync(stored, cancellationToken);
            }

            return;
        }

        var active = await _refreshTokens.BrowseActiveByUserAsync(currentUserId.Value, now, cancellationToken);
        foreach (var token in active)
        {
            token.RevokedAtUtc = now;
            token.RevocationReason = "sign-out";
            await _refreshTokens.UpdateAsync(token, cancellationToken);
        }
    }
}
