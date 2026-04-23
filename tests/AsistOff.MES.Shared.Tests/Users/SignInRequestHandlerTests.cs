using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Multitenancy.Entity;
using AsistOff.MES.Multitenancy.Repositories;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Users.Application.Features.Authentication.SignIn;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Users;

public class SignInRequestHandlerTests
{
    private readonly Mock<IUsersRepository> _users = new();
    private readonly Mock<ITenantRepository> _tenants = new();
    private readonly Mock<IAuthManager> _authManager = new();
    private readonly IPasswordHasher<User> _hasher = new PasswordHasher<User>();

    private SignInRequestHandler CreateSut() =>
        new(_users.Object, _tenants.Object, _hasher, _authManager.Object,
            NullLogger<SignInRequestHandler>.Instance);

    [Fact]
    public async Task Throws_AuthenticationException_when_user_not_found()
    {
        _users.Setup(r => r.GetForAuthenticationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var act = () => CreateSut().Handle(new SignInRequest("missing@example.com", "pw"), CancellationToken.None);

        await act.Should().ThrowAsync<AuthenticationException>().WithMessage("Invalid credentials");
    }

    [Fact]
    public async Task Throws_AuthenticationException_when_password_is_wrong()
    {
        var user = BuildUser("right-password");
        _users.Setup(r => r.GetForAuthenticationAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var act = () => CreateSut().Handle(new SignInRequest(user.Email, "wrong-password"), CancellationToken.None);

        await act.Should().ThrowAsync<AuthenticationException>().WithMessage("Invalid credentials");
    }

    [Fact]
    public async Task Throws_when_tenant_is_inactive()
    {
        var user = BuildUser("pw");
        _users.Setup(r => r.GetForAuthenticationAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _tenants.Setup(r => r.GetByIdAsync(user.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tenant { Id = user.TenantId, Name = "Acme", IsActive = false });

        var act = () => CreateSut().Handle(new SignInRequest(user.Email, "pw"), CancellationToken.None);

        await act.Should().ThrowAsync<AuthenticationException>()
            .Where(e => e.Message.Contains("not active"));
    }

    [Fact]
    public async Task Throws_when_tenant_missing()
    {
        var user = BuildUser("pw");
        _users.Setup(r => r.GetForAuthenticationAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _tenants.Setup(r => r.GetByIdAsync(user.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        var act = () => CreateSut().Handle(new SignInRequest(user.Email, "pw"), CancellationToken.None);

        await act.Should().ThrowAsync<AuthenticationException>();
    }

    [Fact]
    public async Task Issues_token_with_real_tenant_and_permission_claims_on_success()
    {
        var user = BuildUser("pw", isAdmin: true);
        var tenant = new Tenant { Id = user.TenantId, Name = "Acme Factory", IsActive = true, DisplayName = "Acme" };

        _users.Setup(r => r.GetForAuthenticationAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _tenants.Setup(r => r.GetByIdAsync(user.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        Dictionary<string, IEnumerable<string>>? capturedClaims = null;
        _authManager
            .Setup(m => m.CreateToken(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, IEnumerable<string>>>()))
            .Callback<string, string, string, IDictionary<string, IEnumerable<string>>?>((_, _, _, claims) =>
                capturedClaims = claims is null ? null : new Dictionary<string, IEnumerable<string>>(claims))
            .Returns(new JsonWebToken { AccessToken = "token" });

        await CreateSut().Handle(new SignInRequest(user.Email, "pw"), CancellationToken.None);

        capturedClaims.Should().NotBeNull();
        capturedClaims!["tenant_id"].Single().Should().Be(tenant.Id.ToString());
        capturedClaims["tenant_name"].Single().Should().Be("Acme Factory");
        capturedClaims["tenant_active"].Single().Should().Be("true");
        capturedClaims["permissions"].Should().Contain("tenant.admin");
    }

    private User BuildUser(string password, bool isAdmin = false)
    {
        var hasher = new PasswordHasher<User>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "user@example.com",
            Password = string.Empty,
            IsTenantAdmin = isAdmin
        };
        user.Password = hasher.HashPassword(user, password);
        return user;
    }
}
