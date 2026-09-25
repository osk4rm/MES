using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;
using AsistOff.MES.Multitenancy.Context;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Rbac;
using AsistOff.MES.Users.Core.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for issue #210 (RBAC slice 3/3).
/// Proves the role management HTTP surface in the Users module: role
/// lifecycle, permission set replacement, user assignment / unassignment and
/// their effect on the next sign-in permissions claim, plus the failure paths
/// (401 without token, 403 for non-admin callers, 404 for unknown or
/// cross-tenant ids). No EF Core migration is asserted in this slice.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class RolesEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string SignInUrl = "/api/auth/sign-in";
    private const string RolesUrl = "/api/roles";
    private const string PermissionsUrl = "/api/permissions";

    [Fact]
    public async Task CreateRole_ReturnsCreated_AndIsRetrievableById()
    {
        // Arrange
        var (adminEmail, adminPassword) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(adminEmail, adminPassword);
        var code = $"it-{Guid.NewGuid():N}"[..12].ToLowerInvariant();

        // Act
        var create = await client.PostAsJsonAsync(RolesUrl, new
        {
            code,
            name = "Integration role",
            description = "Created by RolesEndpointTests"
        });

        // Assert
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<RoleDto>(create);
        created.Code.Should().Be(code);

        var get = await client.GetAsync($"{RolesUrl}/{created.Id}");

        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await ReadAsync<RoleDetailDto>(get);
        detail.Name.Should().Be("Integration role");
        detail.Members.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateRole_DuplicateCode_Returns409()
    {
        // Arrange
        var (adminEmail, adminPassword) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(adminEmail, adminPassword);
        var code = $"dup-{Guid.NewGuid():N}"[..12].ToLowerInvariant();
        var body = new { code, name = "Duplicate", description = (string?)null };

        // Act
        var first = await client.PostAsJsonAsync(RolesUrl, body);
        var second = await client.PostAsJsonAsync(RolesUrl, body);

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateRole_InvalidBody_Returns400()
    {
        // Arrange
        var (adminEmail, adminPassword) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(adminEmail, adminPassword);

        // Act
        var response = await client.PostAsJsonAsync(RolesUrl, new
        {
            code = "INVALID CODE",
            name = "",
            description = (string?)null
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateRole_UpdatesMetadata_AndIsVisibleOnGet()
    {
        // Arrange
        var (adminEmail, adminPassword) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(adminEmail, adminPassword);
        var created = await CreateRoleAsync(client, "updatable");

        // Act
        var update = await client.PutAsJsonAsync($"{RolesUrl}/{created.Id}", new
        {
            id = created.Id,
            name = "Updated name",
            description = "Updated description"
        });

        // Assert
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await client.GetAsync($"{RolesUrl}/{created.Id}");
        var detail = await ReadAsync<RoleDetailDto>(get);
        detail.Name.Should().Be("Updated name");
        detail.Description.Should().Be("Updated description");
        detail.Code.Should().Be(created.Code);
    }

    [Fact]
    public async Task UpdateRole_UnknownId_Returns404()
    {
        // Arrange
        var (adminEmail, adminPassword) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(adminEmail, adminPassword);
        var id = Guid.NewGuid();

        // Act
        var response = await client.PutAsJsonAsync($"{RolesUrl}/{id}", new
        {
            id,
            name = "Missing",
            description = (string?)null
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AssignAndUnassign_RolePermissionsAreReflectedInNextSignInClaim()
    {
        // Arrange
        var (adminEmail, adminPassword) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);
        var (email, password, userId) = await CreateUserAsync(tenantId);
        using var admin = await Fixture.CreateAuthenticatedClientAsync(adminEmail, adminPassword);

        var role = await CreateRoleAsync(admin, "claimcheck");
        var permissions = await ReadAsync<List<PermissionDto>>(
            await admin.GetAsync(PermissionsUrl));
        var writePermission = permissions.Single(p => p.Code == "users.write");

        // Act — grant users.write and assign the user.
        var set = await admin.PutAsJsonAsync($"{RolesUrl}/{role.Id}/permissions", new
        {
            roleId = role.Id,
            permissionIds = new[] { writePermission.Id }
        });
        set.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var assign = await admin.PostAsJsonAsync($"{RolesUrl}/{role.Id}/members", new
        {
            roleId = role.Id,
            userId
        });
        assign.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert — next sign-in carries the granted permission.
        var token = await SignInAsync(email, password);
        DecodePermissions(token).Should().Contain("users.write");

        // Act — remove the permission from the role.
        var clear = await admin.PutAsJsonAsync($"{RolesUrl}/{role.Id}/permissions", new
        {
            roleId = role.Id,
            permissionIds = Array.Empty<Guid>()
        });
        clear.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert — the following sign-in no longer carries it.
        var refreshed = await SignInAsync(email, password);
        DecodePermissions(refreshed).Should().NotContain("users.write");

        // Act — unassign the user entirely.
        var unassign = await admin.DeleteAsync($"{RolesUrl}/{role.Id}/members/{userId}");

        // Assert
        unassign.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await ReadAsync<RoleDetailDto>(await admin.GetAsync($"{RolesUrl}/{role.Id}"));
        detail.Members.Should().BeEmpty();
    }

    [Fact]
    public async Task RoleEndpoints_WithoutToken_Return401()
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        var get = await client.GetAsync(RolesUrl);
        var post = await client.PostAsJsonAsync(RolesUrl, new
        {
            code = "nope",
            name = "Nope",
            description = (string?)null
        });

        // Assert
        get.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        post.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RoleWrite_AsReadOnlyUser_Returns403()
    {
        // Arrange — caller holds only the read set, lacking tenant.admin.
        var (adminEmail, _) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);
        var (email, password, _) = await CreateUserWithRoleAsync(tenantId, RbacDefaults.UserRoleCode);
        using var client = await ClientForAsync(email, password);
        var unknownId = Guid.NewGuid();

        // Act — every role-management endpoint is tenant-admin only.
        var browse = await client.GetAsync(RolesUrl);
        var get = await client.GetAsync($"{RolesUrl}/{unknownId}");
        var browsePermissions = await client.GetAsync(PermissionsUrl);
        var create = await client.PostAsJsonAsync(RolesUrl, new
        {
            code = "forbidden",
            name = "Forbidden",
            description = (string?)null
        });
        var update = await client.PutAsJsonAsync($"{RolesUrl}/{unknownId}", new
        {
            id = unknownId,
            name = "Forbidden",
            description = (string?)null
        });
        var setPermissions = await client.PutAsJsonAsync($"{RolesUrl}/{unknownId}/permissions", new
        {
            roleId = unknownId,
            permissionIds = Array.Empty<Guid>()
        });
        var assign = await client.PostAsJsonAsync($"{RolesUrl}/{unknownId}/members", new
        {
            roleId = unknownId,
            userId = unknownId
        });
        var unassign = await client.DeleteAsync($"{RolesUrl}/{unknownId}/members/{unknownId}");

        // Assert — authorization runs before the handler, so even unknown ids
        // are rejected with 403 rather than leaking existence via 404.
        browse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        get.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        browsePermissions.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        update.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        setPermissions.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        assign.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        unassign.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CrossTenant_RoleAndUserIds_Return404()
    {
        // Arrange — role and user live in tenant A; caller is tenant B admin.
        var (adminEmailA, adminPasswordA) = await Fixture.CreateTenantAsync();
        var tenantIdA = await GetTenantIdByEmailAsync(adminEmailA);
        using var clientA = await Fixture.CreateAuthenticatedClientAsync(adminEmailA, adminPasswordA);
        var roleA = await CreateRoleAsync(clientA, "tenant-a-role");
        var (_, _, userIdA) = await CreateUserAsync(tenantIdA);

        var (adminEmailB, adminPasswordB) = await Fixture.CreateTenantAsync();
        using var clientB = await Fixture.CreateAuthenticatedClientAsync(adminEmailB, adminPasswordB);
        var roleB = await CreateRoleAsync(clientB, "tenant-b-role");

        // Act
        var getRole = await clientB.GetAsync($"{RolesUrl}/{roleA.Id}");
        var setPermissions = await clientB.PutAsJsonAsync($"{RolesUrl}/{roleA.Id}/permissions", new
        {
            roleId = roleA.Id,
            permissionIds = Array.Empty<Guid>()
        });
        var assignForeignUser = await clientB.PostAsJsonAsync($"{RolesUrl}/{roleB.Id}/members", new
        {
            roleId = roleB.Id,
            userId = userIdA
        });
        var unassignMissing = await clientB.DeleteAsync($"{RolesUrl}/{roleB.Id}/members/{Guid.NewGuid()}");

        // Assert — no data leaks across tenants.
        getRole.StatusCode.Should().Be(HttpStatusCode.NotFound);
        setPermissions.StatusCode.Should().Be(HttpStatusCode.NotFound);
        assignForeignUser.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unassignMissing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<RoleDto> CreateRoleAsync(HttpClient client, string prefix)
    {
        var code = $"{prefix}-{Guid.NewGuid():N}"[..16].ToLowerInvariant().Replace("_", "-");
        var response = await client.PostAsJsonAsync(RolesUrl, new
        {
            code,
            name = $"Role {code}",
            description = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<RoleDto>(response);
    }

    private async Task<string> SignInAsync(string email, string password)
    {
        using var client = Fixture.CreateClient();
        var response = await client.PostAsJsonAsync(SignInUrl, new { email, password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = await ReadAsync<SignInResponse>(response);
        return token.AccessToken;
    }

    private async Task<HttpClient> ClientForAsync(string email, string password)
    {
        var accessToken = await SignInAsync(email, password);
        var client = Fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private async Task<Guid> GetTenantIdByEmailAsync(string email)
    {
        using var scope = Fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MultitenancyDbContext>();
        var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.ContactEmail == email);
        return tenant.Id;
    }

    private async Task<(string Email, string Password, Guid UserId)> CreateUserAsync(
        Guid tenantId, bool isAdmin = false)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"roleuser-{suffix}@integration.local";
        const string password = "Passw0rd!";

        Guid userId;
        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
            var hasher = new PasswordHasher<User>();

            var user = new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Email = email,
                Password = string.Empty,
                IsTenantAdmin = isAdmin
            };
            user.Password = hasher.HashPassword(user, password);
            context.Users.Add(user);
            await context.SaveChangesAsync();
            userId = user.Id;
        }

        return (email, password, userId);
    }

    private async Task<(string Email, string Password, Guid UserId)> CreateUserWithRoleAsync(
        Guid tenantId, string roleCode, bool isAdmin = false)
    {
        var (email, password, userId) = await CreateUserAsync(tenantId, isAdmin);

        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var roles = scope.ServiceProvider.GetRequiredService<IRolesRepository>();
            var role = await roles.GetByCodeAsync(roleCode);
            role.Should().NotBeNull();

            var userRoles = scope.ServiceProvider.GetRequiredService<IUserRolesRepository>();
            await userRoles.AddAsync(new UserRole
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = userId,
                RoleId = role!.Id
            });
        }

        return (email, password, userId);
    }

    private static IReadOnlyCollection<string> DecodePermissions(string accessToken)
    {
        var payload = accessToken.Split('.')[1]
            .Replace('-', '+')
            .Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("permissions", out var permissions))
        {
            return new List<string>();
        }

        // AuthManager emits one Claim per permission, so System.IdentityModel
        // serializes a single-permission token as a JSON string and a
        // multi-permission token as an array. Accept both forms.
        return permissions.ValueKind switch
        {
            JsonValueKind.Array => permissions.EnumerateArray().Select(e => e.GetString()!).ToList(),
            JsonValueKind.String => new List<string> { permissions.GetString()! },
            _ => new List<string>()
        };
    }

    private sealed record SignInResponse(string AccessToken);
}
