using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Rbac;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Users;

public class RbacDefaultsTests
{
    [Fact]
    public void AdminPermissions_MatchInterimModel()
    {
        // Arrange / Act
        var admin = RbacDefaults.AdminPermissions;

        // Assert — parity with SignInRequestHandler.ResolvePermissions admin branch (ADR-0003).
        admin.Should().BeEquivalentTo(
            "users",
            "users.read",
            "users.write",
            "configuration",
            "configuration.read",
            "configuration.write",
            "tenant.admin");
    }

    [Fact]
    public void UserPermissions_MatchInterimModel()
    {
        // Arrange / Act
        var user = RbacDefaults.UserPermissions;

        // Assert — parity with SignInRequestHandler.ResolvePermissions non-admin branch.
        user.Should().BeEquivalentTo(
            "users.read",
            "configuration.read");
    }

    [Fact]
    public void AllPermissionCodes_AreDistinct()
    {
        // Arrange / Act
        var all = RbacDefaults.AllPermissionCodes;

        // Assert
        all.Should().OnlyHaveUniqueItems();
        all.Should().HaveCount(RbacDefaults.AdminPermissions.Union(RbacDefaults.UserPermissions).Distinct().Count());
    }

    [Theory]
    [InlineData("users", "users")]
    [InlineData("users.read", "users")]
    [InlineData("configuration.write", "configuration")]
    [InlineData("tenant.admin", "tenant")]
    public void CategoryFor_DerivesPrefix(string code, string expectedCategory)
    {
        // Act
        var category = RbacDefaults.CategoryFor(code);

        // Assert
        category.Should().Be(expectedCategory);
    }

    [Fact]
    public void RoleCodes_AreDistinct()
    {
        // Assert
        RbacDefaults.AdminRoleCode.Should().NotBe(RbacDefaults.UserRoleCode);
        RbacDefaults.AdminRoleCode.Should().Be("tenant_admin");
        RbacDefaults.UserRoleCode.Should().Be("user");
    }

    [Theory]
    [InlineData(typeof(Role))]
    [InlineData(typeof(Permission))]
    [InlineData(typeof(RolePermission))]
    [InlineData(typeof(UserRole))]
    public void RbacEntities_ImplementTenantAndAuditContracts(Type entityType)
    {
        // Assert — every tenant-scoped entity implements ISaasy (global query filter)
        // and IAuditable (CreatedAt audit), plus IEntity (Guid Id).
        typeof(ISaasy).IsAssignableFrom(entityType).Should().BeTrue($"{entityType.Name} must implement ISaasy");
        typeof(IAuditable).IsAssignableFrom(entityType).Should().BeTrue($"{entityType.Name} must implement IAuditable");
        typeof(IEntity).IsAssignableFrom(entityType).Should().BeTrue($"{entityType.Name} must implement IEntity");
    }

    [Fact]
    public void Role_DefaultCollections_AreInitialized()
    {
        // Arrange / Act
        var role = new Role { Code = "x", Name = "X" };

        // Assert
        role.RolePermissions.Should().NotBeNull();
        role.UserRoles.Should().NotBeNull();
    }

    [Fact]
    public void Permission_DefaultCollections_AreInitialized()
    {
        // Arrange / Act
        var permission = new Permission { Code = "x", Name = "X", Category = "x" };

        // Assert
        permission.RolePermissions.Should().NotBeNull();
    }
}
