using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
/// Endpoint-scoped integration tests for issue #231 (slice 1/2: default-deny write
/// authorization for Users, Multitenancy and Configuration).
/// Proves that an authenticated caller holding only the <c>user</c> role receives
/// 403 on writes (CreateProduct, CreateDepartment) while <c>tenant_admin</c>
/// succeeds, that unauthenticated writes receive 401, and that anonymous sign-in
/// stays reachable. NB: <c>CreateUserRequest</c> has no HTTP endpoint, so its
/// <c>users.write</c> 403 is proven at the pipeline unit level
/// (<c>AuthorizationBehaviorTests.Handle_CreateUserRequest_RequiresUsersWrite</c>).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class WriteAuthorizationEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string SignInUrl = "/api/auth/sign-in";
    private const string ProductsUrl = "/api/products";
    private const string DepartmentsUrl = "/api/departments";

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
    public async Task CreateProduct_AsUserRole_Returns403()
    {
        // Arrange — caller holds only the read set, lacking configuration.write.
        var (adminEmail, _) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);
        var (email, password) = await CreateUserWithRoleAsync(tenantId, RbacDefaults.UserRoleCode);
        using var client = await ClientForAsync(email, password);

        // Act
        var response = await client.PostAsJsonAsync(ProductsUrl, ValidProductBody());

        // Assert — rejected by the default-deny authorization pipeline step.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateProduct_AsTenantAdmin_ReturnsCreated()
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
    public async Task CreateDepartment_AsUserRole_Returns403()
    {
        // Arrange — a second Configuration write, proving coverage generalizes
        // beyond products.
        var (adminEmail, _) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);
        var (email, password) = await CreateUserWithRoleAsync(tenantId, RbacDefaults.UserRoleCode);
        using var client = await ClientForAsync(email, password);

        // Act
        var response = await client.PostAsJsonAsync(DepartmentsUrl, ValidDepartmentBody());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateDepartment_AsTenantAdmin_ReturnsCreated()
    {
        // Arrange
        var (adminEmail, adminPassword) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(adminEmail, adminPassword);

        // Act
        var create = await client.PostAsJsonAsync(DepartmentsUrl, ValidDepartmentBody());

        // Assert
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<DepartmentDto>(create);

        var get = await client.GetAsync($"{DepartmentsUrl}/{created.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SignIn_Anonymous_RemainsReachable()
    {
        // Arrange — sign-in is the documented anonymous bootstrap write.
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(SignInUrl, new
        {
            email = IntegrationTestData.AdminEmail,
            password = IntegrationTestData.AdminPassword
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AuthCookieHelper.GetAccessToken(response).Should().NotBeNullOrWhiteSpace();
    }

    private async Task<string> SignInAsync(string email, string password)
    {
        using var client = Fixture.CreateClient();
        var response = await client.PostAsJsonAsync(SignInUrl, new { email, password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var accessToken = AuthCookieHelper.GetAccessToken(response);
        accessToken.Should().NotBeNullOrWhiteSpace();
        return accessToken!;
    }

    private async Task<HttpClient> ClientForAsync(string email, string password)
    {
        var accessToken = await SignInAsync(email, password);
        var client = Fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private async Task<(string Email, string Password)> CreateUserWithRoleAsync(
        Guid tenantId, string roleCode)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"authz-{suffix}@integration.local";
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
                IsTenantAdmin = false
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
            code = $"AUTHZ-{suffix}",
            name = $"AuthZ product {suffix}",
            description = (string?)null,
            ean = (string?)null,
            barcode = $"AUTHZ-BC-{suffix}",
            scanBy = 1,
            isActive = true,
            productGroupId = (Guid?)null,
            syncId = (string?)null
        };
    }

    private static object ValidDepartmentBody()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        return new
        {
            code = $"AUTHZ-{suffix}",
            name = $"AuthZ department {suffix}"
        };
    }

    private sealed record ProductDto(Guid Id);

    private sealed record DepartmentDto(Guid Id);
}
