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
/// Endpoint-scoped integration tests for issue #209 (RBAC slice 2/3).
/// Proves that sign-in materialises the <c>permissions</c> JWT claim from the
/// caller's role assignments (with parity fallback when no roles are
/// assigned), that the <c>RequirePermission</c> pipeline step rejects
/// unauthorized writes with 403, and that role assignments never leak across
/// tenants. No EF Core migration is asserted in this slice.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class RolePermissionsEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string SignInUrl = "/api/auth/sign-in";
    private const string ProductsUrl = "/api/products";

    [Fact]
    public async Task SignIn_AsTenantAdminWithoutRoleAssignment_IssuesAdminPermissionSet()
    {
        // Arrange — seeded dev admin predates explicit role assignment (fallback path).
        using var client = Fixture.CreateClient();
        var response = await client.PostAsJsonAsync(SignInUrl, new
        {
            email = IntegrationTestData.AdminEmail,
            password = IntegrationTestData.AdminPassword
        });

        // Act
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = await ReadAsync<SignInResponse>(response);

        // Assert — exactly the current admin permission set.
        DecodePermissions(token.AccessToken).Should().BeEquivalentTo(RbacDefaults.AdminPermissions);
    }

    [Fact]
    public async Task SignIn_AsUserWithUserRole_IssuesReadPermissionSet()
    {
        // Arrange
        var (adminEmail, adminPassword) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);
        var (email, password) = await CreateUserWithRoleAsync(tenantId, RbacDefaults.UserRoleCode);

        // Act
        var accessToken = await SignInAsync(email, password);

        // Assert — exactly the current read permission set.
        DecodePermissions(accessToken).Should().BeEquivalentTo(RbacDefaults.UserPermissions);
    }

    [Fact]
    public async Task SignIn_AsUserWithAdminRole_IssuesAdminPermissionSet()
    {
        // Arrange
        var (adminEmail, _) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);
        var (email, password) = await CreateUserWithRoleAsync(tenantId, RbacDefaults.AdminRoleCode);

        // Act
        var accessToken = await SignInAsync(email, password);

        // Assert — exactly the current admin permission set.
        DecodePermissions(accessToken).Should().BeEquivalentTo(RbacDefaults.AdminPermissions);
    }

    [Fact]
    public async Task CreateProduct_AsReadOnlyUser_Returns403()
    {
        // Arrange — caller holds only the read set, lacking configuration.write.
        var (adminEmail, _) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);
        var (email, password) = await CreateUserWithRoleAsync(tenantId, RbacDefaults.UserRoleCode);
        using var client = await ClientForAsync(email, password);

        // Act
        var response = await client.PostAsJsonAsync(ProductsUrl, ValidProductBody());

        // Assert — rejected by the RequirePermission pipeline step.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateProduct_AsAdmin_Succeeds()
    {
        // Arrange
        var (adminEmail, adminPassword) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(adminEmail, adminPassword);

        // Act
        var create = await client.PostAsJsonAsync(ProductsUrl, ValidProductBody());

        // Assert
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ProductDto>(create);

        var get = await client.GetAsync($"{ProductsUrl}/{created.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateProduct_WithoutToken_Returns401()
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(ProductsUrl, ValidProductBody());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CrossTenant_RoleAssignmentsDoNotLeak()
    {
        // Arrange — tenant A user holds only the user role; tenant B admin
        // has no explicit assignment (admin parity fallback).
        var (adminEmailA, _) = await Fixture.CreateTenantAsync();
        var tenantIdA = await GetTenantIdByEmailAsync(adminEmailA);
        var (emailA, passwordA) = await CreateUserWithRoleAsync(tenantIdA, RbacDefaults.UserRoleCode);

        var (adminEmailB, adminPasswordB) = await Fixture.CreateTenantAsync();

        // Act
        var tokenA = await SignInAsync(emailA, passwordA);
        var tokenB = await SignInAsync(adminEmailB, adminPasswordB);

        // Assert — each token carries exactly its own tenant's permission set.
        DecodePermissions(tokenA).Should().BeEquivalentTo(RbacDefaults.UserPermissions);
        DecodePermissions(tokenA).Should().NotContain("tenant.admin");
        DecodePermissions(tokenB).Should().BeEquivalentTo(RbacDefaults.AdminPermissions);
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

    private async Task<(string Email, string Password)> CreateUserWithRoleAsync(
        Guid tenantId, string roleCode, bool isAdmin = false)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"roleuser-{suffix}@integration.local";
        const string password = "Passw0rd!";

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

            var roles = scope.ServiceProvider.GetRequiredService<IRolesRepository>();
            var role = await roles.GetByCodeAsync(roleCode);
            role.Should().NotBeNull();

            var userRoles = scope.ServiceProvider.GetRequiredService<IUserRolesRepository>();
            await userRoles.AddAsync(new UserRole
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = user.Id,
                RoleId = role!.Id
            });
        }

        return (email, password);
    }

    private async Task<Guid> GetTenantIdByEmailAsync(string email)
    {
        using var scope = Fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MultitenancyDbContext>();
        var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.ContactEmail == email);
        return tenant.Id;
    }

    private static object ValidProductBody()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        return new
        {
            code = $"RBAC-{suffix}",
            name = $"RBAC product {suffix}",
            description = (string?)null,
            ean = UniqueEan(),
            barcode = $"RBAC-BC-{suffix}",
            scanBy = 1,
            isActive = true,
            productGroupId = (Guid?)null,
            syncId = (string?)null
        };
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

    /// <summary>Generates a unique, check-digit-valid EAN-13 (issue #212 rejects invalid EANs with 400).</summary>
    private static string UniqueEan()
    {
        var prefix = new char[12];
        prefix[0] = '5';
        prefix[1] = '9';
        prefix[2] = '0';
        for (var i = 3; i < 12; i++)
            prefix[i] = (char)('0' + Random.Shared.Next(10));

        var body = new string(prefix);
        for (var check = 0; check <= 9; check++)
        {
            var candidate = body + check;
            if (HasValidCheckDigit(candidate))
                return candidate;
        }

        throw new InvalidOperationException("Unable to compute an EAN check digit.");
    }

    private static bool HasValidCheckDigit(string value)
    {
        var sum = 0;
        for (var i = 0; i < value.Length; i++)
            sum += (value[i] - '0') * ((value.Length - i) % 2 == 0 ? 3 : 1);

        return sum % 10 == 0;
    }

    private sealed record SignInResponse(string AccessToken);

    private sealed record ProductDto(Guid Id);
}
