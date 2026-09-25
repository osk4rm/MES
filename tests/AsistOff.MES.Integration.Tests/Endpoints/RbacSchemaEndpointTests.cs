using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Multitenancy.Context;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Rbac;
using AsistOff.MES.Users.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Schema-level integration tests for issue #208 (RBAC slice 1/3).
/// There is no HTTP API in this slice, so these tests drive the repositories
/// and <see cref="DefaultContext"/> directly against Testcontainers PostgreSQL,
/// proving the migration, the tenant-scoped unique indexes, the seed parity
/// rows, and the global query filter isolation.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class RbacSchemaEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Migration_Applies_RbacTablesExist()
    {
        // Arrange
        using var scope = Fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();

        // Act — tables are queryable on a fresh migrated database.
        var applied = await context.Database.GetAppliedMigrationsAsync();
        var canQueryRoles = await context.Roles.IgnoreQueryFilters().CountAsync() >= 0;
        var canQueryPermissions = await context.Permissions.IgnoreQueryFilters().CountAsync() >= 0;
        var canQueryRolePermissions = await context.RolePermissions.IgnoreQueryFilters().CountAsync() >= 0;
        var canQueryUserRoles = await context.UserRoles.IgnoreQueryFilters().CountAsync() >= 0;

        // Assert
        applied.Should().Contain(m => m.Contains("AddRbacRolesAndPermissions"));
        canQueryRoles.Should().BeTrue();
        canQueryPermissions.Should().BeTrue();
        canQueryRolePermissions.Should().BeTrue();
        canQueryUserRoles.Should().BeTrue();
    }

    [Fact]
    public async Task SeededRoles_ExistForDevTenant_WithParityPermissions()
    {
        // Arrange
        var tenantId = await GetTenantIdByNameAsync("dev");

        // Act
        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var roles = scope.ServiceProvider.GetRequiredService<IRolesRepository>();
            var rolePermissions = scope.ServiceProvider.GetRequiredService<IRolePermissionsRepository>();

            var admin = await roles.GetByCodeAsync(RbacDefaults.AdminRoleCode);
            var user = await roles.GetByCodeAsync(RbacDefaults.UserRoleCode);

            // Assert — both parity roles exist.
            admin.Should().NotBeNull();
            user.Should().NotBeNull();

            var adminLinks = await rolePermissions.BrowseByRoleAsync(admin!.Id);
            var userLinks = await rolePermissions.BrowseByRoleAsync(user!.Id);

            adminLinks.Select(l => l.Permission!.Code).Should()
                .BeEquivalentTo(RbacDefaults.AdminPermissions);
            userLinks.Select(l => l.Permission!.Code).Should()
                .BeEquivalentTo(RbacDefaults.UserPermissions);
        }
    }

    [Fact]
    public async Task NewTenant_GetsSeededRoles_Automatically()
    {
        // Arrange — provisioning runs via TenantCreatedEventListener + RbacSeeder.
        var (email, _) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(email);

        // Act
        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var roles = scope.ServiceProvider.GetRequiredService<IRolesRepository>();

            var admin = await roles.GetByCodeAsync(RbacDefaults.AdminRoleCode);
            var user = await roles.GetByCodeAsync(RbacDefaults.UserRoleCode);

            // Assert
            admin.Should().NotBeNull();
            user.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task DuplicateRoleCode_SameTenant_Fails()
    {
        // Arrange
        var tenantId = await GetTenantIdByNameAsync("dev");

        // Act
        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var roles = scope.ServiceProvider.GetRequiredService<IRolesRepository>();

            var duplicate = new Role
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = RbacDefaults.AdminRoleCode,
                Name = "Duplicate",
            };

            // Assert — tenant-scoped unique index rejects the duplicate.
            var act = () => roles.AddAsync(duplicate);
            await act.Should().ThrowAsync<DbUpdateException>();
        }
    }

    [Fact]
    public async Task SameRoleCode_DifferentTenants_Succeeds()
    {
        // Arrange
        var devTenantId = await GetTenantIdByNameAsync("dev");
        var (otherEmail, _) = await Fixture.CreateTenantAsync();
        var otherTenantId = await GetTenantIdByEmailAsync(otherEmail);
        var code = $"RBAC-{Guid.NewGuid():N}"[..16];

        // Act — same code in two tenants.
        using (BackgroundTenantContext.BeginScope(devTenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var roles = scope.ServiceProvider.GetRequiredService<IRolesRepository>();
            var created = await roles.AddAsync(new Role
            {
                Id = Guid.NewGuid(),
                TenantId = devTenantId,
                Code = code,
                Name = "Dev role",
            });
            created.Code.Should().Be(code);
        }

        using (BackgroundTenantContext.BeginScope(otherTenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var roles = scope.ServiceProvider.GetRequiredService<IRolesRepository>();
            var created = await roles.AddAsync(new Role
            {
                Id = Guid.NewGuid(),
                TenantId = otherTenantId,
                Code = code,
                Name = "Other role",
            });

            // Assert
            created.Code.Should().Be(code);
        }
    }

    [Fact]
    public async Task CrossTenantReads_ReturnEmpty()
    {
        // Arrange — private role in a fresh tenant.
        var (otherEmail, _) = await Fixture.CreateTenantAsync();
        var otherTenantId = await GetTenantIdByEmailAsync(otherEmail);
        var devTenantId = await GetTenantIdByNameAsync("dev");
        var code = $"ISO-{Guid.NewGuid():N}"[..16];

        using (BackgroundTenantContext.BeginScope(otherTenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var roles = scope.ServiceProvider.GetRequiredService<IRolesRepository>();
            await roles.AddAsync(new Role
            {
                Id = Guid.NewGuid(),
                TenantId = otherTenantId,
                Code = code,
                Name = "Other tenant only",
            });
        }

        // Act — read the same code as the dev tenant.
        using (BackgroundTenantContext.BeginScope(devTenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var roles = scope.ServiceProvider.GetRequiredService<IRolesRepository>();

            var byCode = await roles.GetByCodeAsync(code);
            var browse = await roles.BrowseAsync();

            // Assert — global query filter hides the other tenant's role.
            byCode.Should().BeNull();
            browse.Select(r => r.Code).Should().NotContain(code);
        }
    }

    private async Task<Guid> GetTenantIdByNameAsync(string name)
    {
        using var scope = Fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MultitenancyDbContext>();
        var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.Name == name);
        return tenant.Id;
    }

    private async Task<Guid> GetTenantIdByEmailAsync(string email)
    {
        using var scope = Fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MultitenancyDbContext>();
        var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.ContactEmail == email);
        return tenant.Id;
    }
}
