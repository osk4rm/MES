using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Multitenancy.Entity;
using AsistOff.MES.Multitenancy.Repositories;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Users.Application.Features.Authentication.SignIn;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Rbac;
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
    private readonly Mock<IUserRolesRepository> _userRoles = new();
    private readonly Mock<IRolePermissionsRepository> _rolePermissions = new();
    private readonly Mock<IAuthManager> _authManager = new();
    private readonly IPasswordHasher<User> _hasher = new PasswordHasher<User>();
    private Dictionary<string, IEnumerable<string>>? _capturedClaims;

    private SignInRequestHandler CreateSut()
    {
        _userRoles
            .Setup(r => r.BrowseByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserRole>());

        return new SignInRequestHandler(
            _users.Object, _tenants.Object, _userRoles.Object, _rolePermissions.Object,
            _hasher, _authManager.Object,
            NullLogger<SignInRequestHandler>.Instance);
    }

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
        capturedClaims!["tenant_name"].Single().Should().Be("Acme Factory");
        capturedClaims!["tenant_active"].Single().Should().Be("true");
        capturedClaims!["permissions"].Should().Contain("tenant.admin");
    }

    [Fact]
    public async Task Issues_exact_read_permission_set_for_user_role_assignment()
    {
        // Arrange — non-admin user explicitly assigned only the seeded user role.
        var user = BuildUser("pw", isAdmin: false);
        SetupTenant(user);
        var sut = CreateSut();

        var roleId = Guid.NewGuid();
        _userRoles
            .Setup(r => r.BrowseByUserAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserRole>
            {
                new() { Id = Guid.NewGuid(), TenantId = user.TenantId, UserId = user.Id, RoleId = roleId }
            });
        _rolePermissions
            .Setup(r => r.BrowseByRoleAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RbacDefaults.UserPermissions.Select(code => RolePermissionLink(user.TenantId, roleId, code)).ToList());

        CaptureClaims();

        // Act
        await sut.Handle(new SignInRequest(user.Email, "pw"), CancellationToken.None);

        // Assert — exactly the current read permission set, resolved from role links.
        _capturedClaims.Should().NotBeNull();
        _capturedClaims!["permissions"].Should().BeEquivalentTo(RbacDefaults.UserPermissions);
        _rolePermissions.Verify(r => r.BrowseByRoleAsync(roleId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Issues_exact_admin_permission_set_for_tenant_admin_role_assignment()
    {
        // Arrange — user explicitly assigned only the seeded tenant_admin role.
        var user = BuildUser("pw", isAdmin: true);
        SetupTenant(user);
        var sut = CreateSut();

        var roleId = Guid.NewGuid();
        _userRoles
            .Setup(r => r.BrowseByUserAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserRole>
            {
                new() { Id = Guid.NewGuid(), TenantId = user.TenantId, UserId = user.Id, RoleId = roleId }
            });
        _rolePermissions
            .Setup(r => r.BrowseByRoleAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RbacDefaults.AdminPermissions.Select(code => RolePermissionLink(user.TenantId, roleId, code)).ToList());

        CaptureClaims();

        // Act
        await sut.Handle(new SignInRequest(user.Email, "pw"), CancellationToken.None);

        // Assert — exactly the current admin permission set, resolved from role links.
        _capturedClaims.Should().NotBeNull();
        _capturedClaims!["permissions"].Should().BeEquivalentTo(RbacDefaults.AdminPermissions);
        _rolePermissions.Verify(r => r.BrowseByRoleAsync(roleId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Falls_back_to_read_parity_set_when_user_has_no_role_assignment()
    {
        // Arrange — non-admin user predating the RBAC schema (no UserRole rows).
        var user = BuildUser("pw", isAdmin: false);
        SetupTenant(user);
        var sut = CreateSut();
        CaptureClaims();

        // Act
        await sut.Handle(new SignInRequest(user.Email, "pw"), CancellationToken.None);

        // Assert — parity with the interim non-admin model.
        _capturedClaims.Should().NotBeNull();
        _capturedClaims!["permissions"].Should().BeEquivalentTo(RbacDefaults.UserPermissions);
        _rolePermissions.Verify(
            r => r.BrowseByRoleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Falls_back_to_admin_parity_set_when_admin_has_no_role_assignment()
    {
        // Arrange — tenant admin predating the RBAC schema (no UserRole rows).
        var user = BuildUser("pw", isAdmin: true);
        SetupTenant(user);
        var sut = CreateSut();
        CaptureClaims();

        // Act
        await sut.Handle(new SignInRequest(user.Email, "pw"), CancellationToken.None);

        // Assert — parity with the interim admin model.
        _capturedClaims.Should().NotBeNull();
        _capturedClaims!["permissions"].Should().BeEquivalentTo(RbacDefaults.AdminPermissions);
        _rolePermissions.Verify(
            r => r.BrowseByRoleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void SetupTenant(User user, bool isActive = true)
    {
        _users.Setup(r => r.GetForAuthenticationAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _tenants.Setup(r => r.GetByIdAsync(user.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tenant { Id = user.TenantId, Name = "Acme", IsActive = isActive });
    }

    private void CaptureClaims()
    {
        _authManager
            .Setup(m => m.CreateToken(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, IEnumerable<string>>>()))
            .Callback<string, string, string, IDictionary<string, IEnumerable<string>>?>((_, _, _, claims) =>
                _capturedClaims = claims is null ? null : new Dictionary<string, IEnumerable<string>>(claims))
            .Returns(new JsonWebToken { AccessToken = "token" });
    }

    private static RolePermission RolePermissionLink(Guid tenantId, Guid roleId, string permissionCode) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RoleId = roleId,
            PermissionId = Guid.NewGuid(),
            Permission = new Permission
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = permissionCode,
                Name = permissionCode,
                Category = RbacDefaults.CategoryFor(permissionCode)
            }
        };

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
