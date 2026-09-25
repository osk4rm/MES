using System.Text;
using System.Text.Json;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Multitenancy.Entity;
using AsistOff.MES.Multitenancy.Repositories;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Shared.Infrastructure.Auth;
using AsistOff.MES.Users.Application.Features.Authentication.Refresh;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Users;

/// <summary>
/// Cookie-transport binding for refresh rotation: when the caller presents an
/// ambient access session (header or access cookie) for another tenant than
/// the refresh row, rotation is rejected before any state changes.
/// </summary>
public class RefreshAmbientBindingTests
{
    private readonly Mock<IRefreshTokensRepository> _refreshTokens = new();
    private readonly Mock<IUsersRepository> _users = new();
    private readonly Mock<ITenantRepository> _tenants = new();
    private readonly Mock<IUserRolesRepository> _userRoles = new();
    private readonly Mock<IRolePermissionsRepository> _rolePermissions = new();
    private readonly Mock<IAuthManager> _authManager = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly DateTime _now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    public RefreshAmbientBindingTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _userRoles.Setup(r => r.BrowseByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserRole>());
        _authManager.Setup(m => m.CreateToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IDictionary<string, IEnumerable<string>>>()))
            .Returns<string, string, string, IDictionary<string, IEnumerable<string>>?>((userId, role, _, _) =>
                new JsonWebToken { AccessToken = "access", Id = userId, Role = role ?? string.Empty });
    }

    [Fact]
    public async Task Refresh_MatchingAmbientTenant_Rotates()
    {
        // Arrange — ambient session and refresh row belong to the same tenant.
        var setup = ArrangeStoredRow();
        var ambient = MintAmbientToken(setup.TenantId);
        var sut = BuildHandler();

        // Act
        var result = await sut.Handle(new RefreshTokenRequest(setup.Opaque, ambient), CancellationToken.None);

        // Assert — normal rotation.
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBe(setup.Opaque);
        setup.Stored.RevokedAtUtc.Should().Be(_now);
    }

    [Fact]
    public async Task Refresh_MismatchedAmbientTenant_ThrowsWithoutStateChange()
    {
        // Arrange — refresh cookie from tenant A, ambient session from tenant B.
        var setup = ArrangeStoredRow();
        var ambient = MintAmbientToken(Guid.NewGuid());
        var sut = BuildHandler();

        // Act
        var act = () => sut.Handle(new RefreshTokenRequest(setup.Opaque, ambient), CancellationToken.None);

        // Assert — rejected, and the stored token is untouched so the legitimate
        // owner can still rotate anonymously afterwards.
        await act.Should().ThrowAsync<AuthenticationException>();
        setup.Stored.RevokedAtUtc.Should().BeNull();
        setup.Stored.ReplacedByHash.Should().BeNull();
        _refreshTokens.Verify(r => r.UpdateAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);

        var retry = await sut.Handle(new RefreshTokenRequest(setup.Opaque), CancellationToken.None);
        retry.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Refresh_UnparseableAmbientToken_TreatedAsAnonymous()
    {
        // Arrange — stale foreign token that is not a JWT at all.
        var setup = ArrangeStoredRow();
        var sut = BuildHandler();

        // Act
        var result = await sut.Handle(new RefreshTokenRequest(setup.Opaque, "not-a-jwt"), CancellationToken.None);

        // Assert — the opaque refresh token remains the credential.
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Refresh_AmbientTokenWithoutTenantClaim_TreatedAsAnonymous()
    {
        // Arrange — well-formed JWT payload but no tenant_id claim.
        var setup = ArrangeStoredRow();
        var payload = Base64Url(JsonSerializer.Serialize(new { sub = "someone" }));
        var sut = BuildHandler();

        // Act
        var result = await sut.Handle(new RefreshTokenRequest(setup.Opaque, $"header.{payload}.sig"), CancellationToken.None);

        // Assert
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Refresh_NullRefreshToken_Throws()
    {
        // Arrange — neither body nor cookie supplied a token.
        var sut = BuildHandler();

        // Act
        var act = () => sut.Handle(new RefreshTokenRequest(null), CancellationToken.None);

        // Assert — auth semantics (401), not validation (400).
        await act.Should().ThrowAsync<AuthenticationException>();
    }

    private sealed record StoredSetup(Guid TenantId, string Opaque, RefreshToken Stored);

    private StoredSetup ArrangeStoredRow()
    {
        var tenantId = Guid.NewGuid();
        var user = BuildUser(tenantId);
        var tenant = new Tenant { Id = tenantId, Name = "Acme", IsActive = true };
        var (opaque, hash) = RefreshTokenHasher.Generate();
        var stored = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = user.Id,
            TokenHash = hash,
            FamilyId = Guid.NewGuid(),
            ExpiresAtUtc = _now.AddHours(1),
            CreatedAt = _now.AddMinutes(-5)
        };

        _refreshTokens.Setup(r => r.GetByHashIgnoringQueryFiltersAsync(hash, It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        _users.Setup(r => r.GetAsync(user.Id)).ReturnsAsync(user);
        _tenants.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        _refreshTokens.Setup(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        return new StoredSetup(tenantId, opaque, stored);
    }

    private RefreshTokenRequestHandler BuildHandler() => new(
        _refreshTokens.Object, _users.Object, _tenants.Object,
        _userRoles.Object, _rolePermissions.Object, _authManager.Object,
        new AuthOptions
        {
            IssuerSigningKey = new string('k', 40),
            Issuer = "AsistOff.MES",
            Audience = "AsistOff.MES.Users",
            RefreshTokenLifetime = TimeSpan.FromDays(7)
        },
        _clock.Object, _guids.Object,
        NullLogger<RefreshTokenRequestHandler>.Instance);

    private static User BuildUser(Guid tenantId)
    {
        var hasher = new PasswordHasher<User>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "user@example.com",
            Password = string.Empty,
            IsTenantAdmin = true
        };
        user.Password = hasher.HashPassword(user, "pw");
        return user;
    }

    private static string MintAmbientToken(Guid tenantId)
        => $"header.{Base64Url(JsonSerializer.Serialize(new { tenant_id = tenantId.ToString() }))}.sig";

    private static string Base64Url(string value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
