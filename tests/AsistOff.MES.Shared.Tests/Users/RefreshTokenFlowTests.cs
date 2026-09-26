using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Multitenancy.Entity;
using AsistOff.MES.Multitenancy.Repositories;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Shared.Infrastructure.Auth;
using AsistOff.MES.Users.Application.Features.Authentication.Refresh;
using AsistOff.MES.Users.Application.Features.Authentication.SignIn;
using AsistOff.MES.Users.Application.Features.Authentication.SignOut;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Users;

public class RefreshTokenFlowTests
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

    public RefreshTokenFlowTests()
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

    private AuthOptions AuthOptions() => new()
    {
        IssuerSigningKey = new string('k', 40),
        Issuer = "AsistOff.MES",
        Audience = "AsistOff.MES.Users",
        RefreshTokenLifetime = TimeSpan.FromDays(7)
    };

    private User BuildUser(string password, Guid? tenantId = null, bool isAdmin = true)
    {
        var hasher = new PasswordHasher<User>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId ?? Guid.NewGuid(),
            Email = "user@example.com",
            Password = string.Empty,
            IsTenantAdmin = isAdmin
        };
        user.Password = hasher.HashPassword(user, password);
        return user;
    }

    private RefreshTokenRequestHandler BuildRefreshHandler() => new(
        _refreshTokens.Object, _users.Object, _tenants.Object,
        _userRoles.Object, _rolePermissions.Object, _authManager.Object,
        AuthOptions(), _clock.Object, _guids.Object,
        NullLogger<RefreshTokenRequestHandler>.Instance);

    [Fact]
    public async Task SignIn_IssuesOpaqueRefreshToken_PersistedServerSide()
    {
        // Arrange
        var user = BuildUser("pw");
        var tenant = new Tenant { Id = user.TenantId, Name = "Acme", IsActive = true };
        _users.Setup(r => r.GetForAuthenticationAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _tenants.Setup(r => r.GetByIdAsync(user.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        RefreshToken? persisted = null;
        _refreshTokens.Setup(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshToken, CancellationToken>((t, _) => persisted = t)
            .Returns(Task.CompletedTask);
        var sut = new SignInRequestHandler(
            _users.Object, _tenants.Object, _userRoles.Object, _rolePermissions.Object,
            new PasswordHasher<User>(), _authManager.Object,
            NullLogger<SignInRequestHandler>.Instance,
            _refreshTokens.Object, AuthOptions(), _clock.Object, _guids.Object);

        // Act
        var result = await sut.Handle(new SignInRequest(user.Email, "pw"), CancellationToken.None);

        // Assert
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        persisted.Should().NotBeNull();
        persisted!.UserId.Should().Be(user.Id);
        persisted.TenantId.Should().Be(user.TenantId);
        persisted.TokenHash.Should().Be(RefreshTokenHasher.Hash(result.RefreshToken));
        persisted.ExpiresAtUtc.Should().Be(_now.Add(TimeSpan.FromDays(7)));
    }

    [Fact]
    public async Task Refresh_Rotates_InvalidatesOldAndIssuesNew()
    {
        // Arrange — no ambient tenant or user: the endpoint is anonymous and the
        // tenant comes from the stored row.
        var user = BuildUser("pw");
        var tenant = new Tenant { Id = user.TenantId, Name = "Acme", IsActive = true };
        var (opaque, hash) = RefreshTokenHasher.Generate();
        var stored = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TenantId = user.TenantId,
            UserId = user.Id,
            TokenHash = hash,
            FamilyId = Guid.NewGuid(),
            ExpiresAtUtc = _now.AddHours(1),
            CreatedAt = _now.AddMinutes(-5)
        };
        _refreshTokens.Setup(r => r.GetByHashIgnoringQueryFiltersAsync(hash, It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        _users.Setup(r => r.GetAsync(user.Id)).ReturnsAsync(user);
        _tenants.Setup(r => r.GetByIdAsync(user.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        RefreshToken? successor = null;
        _refreshTokens.Setup(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshToken, CancellationToken>((t, _) => successor = t)
            .Returns(Task.CompletedTask);

        var sut = BuildRefreshHandler();

        // Act
        var result = await sut.Handle(new RefreshTokenRequest(opaque), CancellationToken.None);

        // Assert — old invalidated, new issued in the same family and tenant.
        stored.RevokedAtUtc.Should().Be(_now);
        stored.ReplacedByHash.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBe(opaque);
        successor.Should().NotBeNull();
        successor!.FamilyId.Should().Be(stored.FamilyId);
        successor.TenantId.Should().Be(stored.TenantId);
        successor.TokenHash.Should().Be(RefreshTokenHasher.Hash(result.RefreshToken));
        _refreshTokens.Verify(r => r.UpdateAsync(stored, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Refresh_BindsSessionToStoredTenant_NotCallerInput()
    {
        // Arrange — the minted access token must carry the stored row's tenant,
        // even though the request itself carries no tenant information.
        var user = BuildUser("pw");
        var tenant = new Tenant { Id = user.TenantId, Name = "Acme", IsActive = true };
        var (opaque, hash) = RefreshTokenHasher.Generate();
        var stored = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TenantId = user.TenantId,
            UserId = user.Id,
            TokenHash = hash,
            FamilyId = Guid.NewGuid(),
            ExpiresAtUtc = _now.AddHours(1),
            CreatedAt = _now.AddMinutes(-5)
        };
        _refreshTokens.Setup(r => r.GetByHashIgnoringQueryFiltersAsync(hash, It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        _users.Setup(r => r.GetAsync(user.Id)).ReturnsAsync(user);
        _tenants.Setup(r => r.GetByIdAsync(user.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        _refreshTokens.Setup(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        IDictionary<string, IEnumerable<string>>? capturedClaims = null;
        _authManager.Setup(m => m.CreateToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IDictionary<string, IEnumerable<string>>>()))
            .Callback<string, string, string, IDictionary<string, IEnumerable<string>>?>((_, _, _, c) => capturedClaims = c)
            .Returns<string, string, string, IDictionary<string, IEnumerable<string>>?>((userId, role, _, _) =>
                new JsonWebToken { AccessToken = "access", Id = userId, Role = role ?? string.Empty });

        var sut = BuildRefreshHandler();

        // Act
        await sut.Handle(new RefreshTokenRequest(opaque), CancellationToken.None);

        // Assert — tenant claim equals the stored row's tenant.
        capturedClaims.Should().NotBeNull();
        capturedClaims!["tenant_id"].Should().Equal(user.TenantId.ToString());
    }

    [Fact]
    public async Task Refresh_UserTenantMismatch_Throws()
    {
        // Arrange — the user moved to (or belongs to) another tenant: the token
        // must not mint a session for them.
        var user = BuildUser("pw", tenantId: Guid.NewGuid());
        var (opaque, hash) = RefreshTokenHasher.Generate();
        var stored = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = hash,
            FamilyId = Guid.NewGuid(),
            ExpiresAtUtc = _now.AddHours(1),
            CreatedAt = _now.AddMinutes(-5)
        };
        _refreshTokens.Setup(r => r.GetByHashIgnoringQueryFiltersAsync(hash, It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        _users.Setup(r => r.GetAsync(user.Id)).ReturnsAsync(user);

        var sut = BuildRefreshHandler();

        // Act
        var act = () => sut.Handle(new RefreshTokenRequest(opaque), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AuthenticationException>();
    }

    [Fact]
    public async Task Refresh_UnknownToken_Throws()
    {
        // Arrange
        _refreshTokens.Setup(r => r.GetByHashIgnoringQueryFiltersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);

        var sut = BuildRefreshHandler();

        // Act
        var act = () => sut.Handle(new RefreshTokenRequest("bogus"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AuthenticationException>();
    }

    [Fact]
    public async Task Refresh_EmptyToken_Throws()
    {
        // Arrange
        var sut = BuildRefreshHandler();

        // Act
        var act = () => sut.Handle(new RefreshTokenRequest(string.Empty), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AuthenticationException>();
    }

    [Fact]
    public async Task Refresh_ExpiredToken_Throws()
    {
        // Arrange — the stored token ran past its expiry.
        var user = BuildUser("pw");
        var (opaque, hash) = RefreshTokenHasher.Generate();
        var stored = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TenantId = user.TenantId,
            UserId = user.Id,
            TokenHash = hash,
            FamilyId = Guid.NewGuid(),
            ExpiresAtUtc = _now.AddMinutes(-1),
            CreatedAt = _now.AddDays(-8)
        };
        _refreshTokens.Setup(r => r.GetByHashIgnoringQueryFiltersAsync(hash, It.IsAny<CancellationToken>())).ReturnsAsync(stored);

        var sut = BuildRefreshHandler();

        // Act
        var act = () => sut.Handle(new RefreshTokenRequest(opaque), CancellationToken.None);

        // Assert — expired sessions are rejected with 401, never rotated.
        await act.Should().ThrowAsync<AuthenticationException>();
        _refreshTokens.Verify(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Refresh_ReuseOfRotatedToken_RevokesFamilyAndThrows()
    {
        // Arrange — presenting an already-rotated token is a reuse attack.
        var user = BuildUser("pw");
        var (opaque, hash) = RefreshTokenHasher.Generate();
        var familyId = Guid.NewGuid();
        var stored = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TenantId = user.TenantId,
            UserId = user.Id,
            TokenHash = hash,
            FamilyId = familyId,
            ExpiresAtUtc = _now.AddHours(1),
            CreatedAt = _now.AddMinutes(-10),
            RevokedAtUtc = _now.AddMinutes(-1),
            ReplacedByHash = "newhash",
            RevocationReason = "rotated"
        };
        var sibling = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TenantId = user.TenantId,
            UserId = user.Id,
            TokenHash = "newhash",
            FamilyId = familyId,
            ExpiresAtUtc = _now.AddDays(7),
            CreatedAt = _now.AddMinutes(-1)
        };
        _refreshTokens.Setup(r => r.GetByHashIgnoringQueryFiltersAsync(hash, It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        _refreshTokens.Setup(r => r.BrowseByFamilyAsync(familyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RefreshToken> { stored, sibling });

        var sut = BuildRefreshHandler();

        // Act
        var act = () => sut.Handle(new RefreshTokenRequest(opaque), CancellationToken.None);

        // Assert — reuse rejected and the whole family revoked.
        await act.Should().ThrowAsync<AuthenticationException>();
        sibling.RevokedAtUtc.Should().Be(_now);
        sibling.RevocationReason.Should().Be("reuse-detected");
    }

    [Fact]
    public async Task SignOut_RevokesToken_SoRefreshFails()
    {
        // Arrange
        var user = BuildUser("pw");
        var (opaque, hash) = RefreshTokenHasher.Generate();
        var stored = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TenantId = user.TenantId,
            UserId = user.Id,
            TokenHash = hash,
            FamilyId = Guid.NewGuid(),
            ExpiresAtUtc = _now.AddDays(7),
            CreatedAt = _now
        };
        _refreshTokens.Setup(r => r.GetByHashAsync(hash, It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        _refreshTokens.Setup(r => r.GetByHashIgnoringQueryFiltersAsync(hash, It.IsAny<CancellationToken>())).ReturnsAsync(stored);

        var tenantAccessor = new Mock<ICurrentTenantAccessor>();
        tenantAccessor.Setup(t => t.TryGetTenantId(out It.Ref<Guid>.IsAny)).Returns((out Guid id) =>
        {
            id = user.TenantId;
            return true;
        });
        var userAccessor = new Mock<ICurrentUserAccessor>();
        userAccessor.SetupGet(u => u.UserId).Returns(user.Id);

        var signOut = new SignOutRequestHandler(
            _refreshTokens.Object, _clock.Object,
            tenantAccessor.Object, userAccessor.Object,
            NullLogger<SignOutRequestHandler>.Instance);

        // Act — sign out revokes.
        await signOut.Handle(new SignOutRequest(opaque), CancellationToken.None);

        // Assert — revoked; a subsequent refresh with the same token is rejected.
        stored.RevokedAtUtc.Should().Be(_now);
        stored.RevocationReason.Should().Be("sign-out");

        var refresh = BuildRefreshHandler();
        var act = () => refresh.Handle(new RefreshTokenRequest(opaque), CancellationToken.None);
        await act.Should().ThrowAsync<AuthenticationException>();
    }
}
