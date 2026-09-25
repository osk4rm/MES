using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Users.Application.Features.Roles.Assign;
using AsistOff.MES.Users.Application.Features.Roles.Browse;
using AsistOff.MES.Users.Application.Features.Roles.Create;
using AsistOff.MES.Users.Application.Features.Roles.Get;
using AsistOff.MES.Users.Application.Features.Roles.SetPermissions;
using AsistOff.MES.Users.Application.Features.Roles.Unassign;
using AsistOff.MES.Users.Application.Features.Roles.Update;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Repositories;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Users;

/// <summary>
/// Handler tests for the issue #210 role management slice: role create and
/// update, permission set replacement, and user assignment / unassignment.
/// Repositories are mocked; no database is used.
/// </summary>
public class RoleManagementHandlerTests
{
    private readonly Mock<IRolesRepository> _roles = new();
    private readonly Mock<IPermissionsRepository> _permissions = new();
    private readonly Mock<IRolePermissionsRepository> _rolePermissions = new();
    private readonly Mock<IUserRolesRepository> _userRoles = new();
    private readonly Mock<IUsersRepository> _users = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    public RoleManagementHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
    }

    [Fact]
    public async Task CreateRole_DuplicateCode_ThrowsConflictException()
    {
        // Arrange
        _roles.Setup(r => r.GetByCodeAsync("quality", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildRole("quality"));

        var handler = new CreateRoleRequestHandler(_roles.Object, _guids.Object, _tenant.Object);

        // Act
        var act = () => handler.Handle(new CreateRoleRequest("quality", "Quality", null), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task CreateRole_ValidRequest_PersistsTenantIdAndReturnsResponse()
    {
        // Arrange
        _roles.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Role?)null);

        Role? persisted = null;
        _roles.Setup(r => r.AddAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()))
            .Callback<Role, CancellationToken>((role, _) => persisted = role)
            .ReturnsAsync((Role role, CancellationToken _) => role);

        var handler = new CreateRoleRequestHandler(_roles.Object, _guids.Object, _tenant.Object);

        // Act
        var result = await handler.Handle(
            new CreateRoleRequest("quality", " Quality lead ", " desc "), CancellationToken.None);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.TenantId.Should().Be(_tenantId);
        persisted.Code.Should().Be("quality");
        result.Code.Should().Be("quality");
        result.Name.Should().Be("Quality lead");
        result.PermissionCodes.Should().BeEmpty();
        result.MemberCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateRole_UnknownId_ThrowsNotFoundException()
    {
        // Arrange
        _roles.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Role?)null);

        var handler = new UpdateRoleRequestHandler(_roles.Object);

        // Act
        var act = () => handler.Handle(
            new UpdateRoleRequest(Guid.NewGuid(), "Quality", null), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateRole_ValidRequest_UpdatesMetadataOnly()
    {
        // Arrange
        var role = BuildRole("quality");
        _roles.Setup(r => r.GetByIdAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        var handler = new UpdateRoleRequestHandler(_roles.Object);

        // Act
        await handler.Handle(new UpdateRoleRequest(role.Id, "Quality lead", " leads "), CancellationToken.None);

        // Assert
        role.Code.Should().Be("quality");
        role.Name.Should().Be("Quality lead");
        role.Description.Should().Be("leads");
        _roles.Verify(r => r.UpdateAsync(role, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetRolePermissions_UnknownRole_ThrowsNotFoundException()
    {
        // Arrange
        _roles.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Role?)null);

        var handler = new SetRolePermissionsRequestHandler(
            _roles.Object, _permissions.Object, _rolePermissions.Object, _guids.Object);

        // Act
        var act = () => handler.Handle(
            new SetRolePermissionsRequest(Guid.NewGuid(), [Guid.NewGuid()]), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task SetRolePermissions_UnknownPermission_ThrowsNotFoundException()
    {
        // Arrange
        var role = BuildRole("quality");
        _roles.Setup(r => r.GetByIdAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        _permissions.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Permission?)null);

        var handler = new SetRolePermissionsRequestHandler(
            _roles.Object, _permissions.Object, _rolePermissions.Object, _guids.Object);

        // Act
        var act = () => handler.Handle(
            new SetRolePermissionsRequest(role.Id, [Guid.NewGuid()]), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task SetRolePermissions_ReplacesCollection_AddsNewAndRemovesStale()
    {
        // Arrange
        var role = BuildRole("quality");
        var keep = BuildPermission("users.read");
        var stale = BuildPermission("configuration.read");
        var added = BuildPermission("users.write");

        _roles.Setup(r => r.GetByIdAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        _permissions.Setup(r => r.GetByIdAsync(keep.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(keep);
        _permissions.Setup(r => r.GetByIdAsync(added.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(added);

        var keepLink = new RolePermission { Id = Guid.NewGuid(), TenantId = _tenantId, RoleId = role.Id, PermissionId = keep.Id, Permission = keep };
        var staleLink = new RolePermission { Id = Guid.NewGuid(), TenantId = _tenantId, RoleId = role.Id, PermissionId = stale.Id, Permission = stale };
        _rolePermissions.Setup(r => r.BrowseByRoleAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RolePermission> { keepLink, staleLink });

        var handler = new SetRolePermissionsRequestHandler(
            _roles.Object, _permissions.Object, _rolePermissions.Object, _guids.Object);

        // Act
        await handler.Handle(new SetRolePermissionsRequest(role.Id, [keep.Id, added.Id]), CancellationToken.None);

        // Assert — stale link removed, kept link untouched, new link added.
        _rolePermissions.Verify(r => r.RemoveAsync(staleLink, It.IsAny<CancellationToken>()), Times.Once);
        _rolePermissions.Verify(r => r.RemoveAsync(keepLink, It.IsAny<CancellationToken>()), Times.Never);
        _rolePermissions.Verify(r => r.AddAsync(
            It.Is<RolePermission>(x => x.RoleId == role.Id && x.PermissionId == added.Id && x.TenantId == role.TenantId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignUserToRole_UnknownUser_ThrowsNotFoundException()
    {
        // Arrange
        var role = BuildRole("quality");
        _roles.Setup(r => r.GetByIdAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        _users.Setup(r => r.GetAsync(It.IsAny<Guid>()))
            .ReturnsAsync((User?)null);

        var handler = new AssignUserToRoleRequestHandler(
            _roles.Object, _users.Object, _userRoles.Object, _guids.Object);

        // Act
        var act = () => handler.Handle(
            new AssignUserToRoleRequest(role.Id, Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task AssignUserToRole_AlreadyAssigned_IsIdempotent()
    {
        // Arrange
        var role = BuildRole("quality");
        var user = BuildUser();
        _roles.Setup(r => r.GetByIdAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        _users.Setup(r => r.GetAsync(user.Id)).ReturnsAsync(user);
        _userRoles.Setup(r => r.BrowseByUserAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserRole>
            {
                new() { Id = Guid.NewGuid(), TenantId = _tenantId, UserId = user.Id, RoleId = role.Id }
            });

        var handler = new AssignUserToRoleRequestHandler(
            _roles.Object, _users.Object, _userRoles.Object, _guids.Object);

        // Act
        await handler.Handle(new AssignUserToRoleRequest(role.Id, user.Id), CancellationToken.None);

        // Assert
        _userRoles.Verify(r => r.AddAsync(It.IsAny<UserRole>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssignUserToRole_NewAssignment_PersistsLink()
    {
        // Arrange
        var role = BuildRole("quality");
        var user = BuildUser();
        _roles.Setup(r => r.GetByIdAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        _users.Setup(r => r.GetAsync(user.Id)).ReturnsAsync(user);
        _userRoles.Setup(r => r.BrowseByUserAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserRole>());

        UserRole? persisted = null;
        _userRoles.Setup(r => r.AddAsync(It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .Callback<UserRole, CancellationToken>((link, _) => persisted = link)
            .ReturnsAsync((UserRole link, CancellationToken _) => link);

        var handler = new AssignUserToRoleRequestHandler(
            _roles.Object, _users.Object, _userRoles.Object, _guids.Object);

        // Act
        await handler.Handle(new AssignUserToRoleRequest(role.Id, user.Id), CancellationToken.None);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.RoleId.Should().Be(role.Id);
        persisted.UserId.Should().Be(user.Id);
        persisted.TenantId.Should().Be(role.TenantId);
    }

    [Fact]
    public async Task UnassignUserFromRole_MissingAssignment_ThrowsNotFoundException()
    {
        // Arrange
        var role = BuildRole("quality");
        _roles.Setup(r => r.GetByIdAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        _userRoles.Setup(r => r.BrowseByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserRole>());

        var handler = new UnassignUserFromRoleRequestHandler(_roles.Object, _userRoles.Object);

        // Act
        var act = () => handler.Handle(
            new UnassignUserFromRoleRequest(role.Id, Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UnassignUserFromRole_ExistingAssignment_RemovesLink()
    {
        // Arrange
        var role = BuildRole("quality");
        var userId = Guid.NewGuid();
        var assignment = new UserRole
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, UserId = userId, RoleId = role.Id
        };

        _roles.Setup(r => r.GetByIdAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        _userRoles.Setup(r => r.BrowseByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserRole> { assignment });

        var handler = new UnassignUserFromRoleRequestHandler(_roles.Object, _userRoles.Object);

        // Act
        await handler.Handle(new UnassignUserFromRoleRequest(role.Id, userId), CancellationToken.None);

        // Assert
        _userRoles.Verify(r => r.RemoveAsync(assignment, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BrowseRoles_MapsPermissionCodesAndMemberCounts()
    {
        // Arrange
        var admin = BuildRole("tenant_admin");
        var user = BuildRole("user");
        _roles.Setup(r => r.BrowseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Role> { admin, user });

        var readPermission = BuildPermission("users.read");
        _rolePermissions.Setup(r => r.BrowseByRoleAsync(admin.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RolePermission>
            {
                new() { Id = Guid.NewGuid(), TenantId = _tenantId, RoleId = admin.Id, PermissionId = readPermission.Id, Permission = readPermission }
            });
        _rolePermissions.Setup(r => r.BrowseByRoleAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RolePermission>());

        _userRoles.Setup(r => r.BrowseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserRole>
            {
                new() { Id = Guid.NewGuid(), TenantId = _tenantId, UserId = Guid.NewGuid(), RoleId = admin.Id },
                new() { Id = Guid.NewGuid(), TenantId = _tenantId, UserId = Guid.NewGuid(), RoleId = admin.Id }
            });

        var handler = new BrowseRolesRequestHandler(_roles.Object, _rolePermissions.Object, _userRoles.Object);

        // Act
        var result = await handler.Handle(new BrowseRolesRequest(), CancellationToken.None);

        // Assert
        var adminRow = result.Single(x => x.Code == "tenant_admin");
        adminRow.PermissionCodes.Should().Contain("users.read");
        adminRow.MemberCount.Should().Be(2);

        var userRow = result.Single(x => x.Code == "user");
        userRow.PermissionCodes.Should().BeEmpty();
        userRow.MemberCount.Should().Be(0);
    }

    [Fact]
    public async Task GetRole_UnknownId_ThrowsNotFoundException()
    {
        // Arrange
        _roles.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Role?)null);

        var handler = new GetRoleRequestHandler(
            _roles.Object, _rolePermissions.Object, _userRoles.Object, _users.Object);

        // Act
        var act = () => handler.Handle(new GetRoleRequest(Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetRole_ValidId_ReturnsPermissionsAndMembers()
    {
        // Arrange
        var role = BuildRole("quality");
        var permission = BuildPermission("users.read");
        var member = BuildUser("member@example.com");

        _roles.Setup(r => r.GetByIdAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        _rolePermissions.Setup(r => r.BrowseByRoleAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RolePermission>
            {
                new() { Id = Guid.NewGuid(), TenantId = _tenantId, RoleId = role.Id, PermissionId = permission.Id, Permission = permission }
            });
        _userRoles.Setup(r => r.BrowseByRoleAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserRole>
            {
                new() { Id = Guid.NewGuid(), TenantId = _tenantId, UserId = member.Id, RoleId = role.Id, User = member }
            });

        var handler = new GetRoleRequestHandler(
            _roles.Object, _rolePermissions.Object, _userRoles.Object, _users.Object);

        // Act
        var result = await handler.Handle(new GetRoleRequest(role.Id), CancellationToken.None);

        // Assert
        result.Code.Should().Be("quality");
        result.Permissions.Should().ContainSingle(x => x.Code == "users.read");
        result.Members.Should().ContainSingle(x => x.UserId == member.Id && x.Email == member.Email);
    }

    private Role BuildRole(string code) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Code = code,
            Name = code,
            Description = null
        };

    private Permission BuildPermission(string code) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Code = code,
            Name = code,
            Category = "users"
        };

    private User BuildUser(string email = "user@example.com") =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Email = email,
            Password = "hash"
        };
}
