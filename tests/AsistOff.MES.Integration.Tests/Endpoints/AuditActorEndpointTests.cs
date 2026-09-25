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
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Slice 1 (#245) actor coverage: creating a Production Order persists the
/// caller as <c>CreatedBy</c>, updating a Machine as a second user stamps
/// <c>ModifiedBy</c>, anonymous tenant provisioning succeeds with null
/// actors, and cross-tenant reads never leak actor ids.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class AuditActorEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string OrdersUrl = "/api/production-orders";
    private const string MachinesUrl = "/api/machines";
    private const string SignInUrl = "/api/auth/sign-in";

    [Fact]
    public async Task CreateProductionOrder_PersistsCallerAsCreatedBy()
    {
        // Arrange
        var token = await SignInAsync(IntegrationTestData.AdminEmail, IntegrationTestData.AdminPassword);
        var callerId = DecodeSub(token);
        using var client = ClientWithToken(token);

        // Act
        var created = await CreateOrderAsync(client);

        // Assert — detail read exposes the actor.
        created.CreatedBy.Should().Be(callerId);
        created.ModifiedBy.Should().BeNull();

        var fetched = await ReadAsync<ProductionOrderDto>(
            await client.GetAsync($"{OrdersUrl}/{created.Id}"));
        fetched.CreatedBy.Should().Be(callerId);
        fetched.ModifiedBy.Should().BeNull();
    }

    [Fact]
    public async Task UpdateMachine_AsSecondUser_SetsModifiedByKeepsCreatedBy()
    {
        // Arrange — isolated tenant so the two admins are unambiguous.
        var (adminEmail, adminPassword) = await Fixture.CreateTenantAsync();
        var adminToken = await SignInAsync(adminEmail, adminPassword);
        var firstAdminId = DecodeSub(adminToken);
        using var admin = ClientWithToken(adminToken);
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);

        var machine = await CreateMachineAsync(admin);
        machine.CreatedBy.Should().Be(firstAdminId);
        machine.ModifiedBy.Should().BeNull();

        var (secondEmail, secondPassword, secondUserId) = await CreateUserAsync(tenantId, isAdmin: true);
        var secondToken = await SignInAsync(secondEmail, secondPassword);
        DecodeSub(secondToken).Should().Be(secondUserId);
        using var second = ClientWithToken(secondToken);

        // Act
        var update = await second.PutAsJsonAsync($"{MachinesUrl}/{machine.Id}", new
        {
            id = machine.Id,
            code = machine.Code,
            name = machine.Name,
            description = machine.Description,
            isActive = machine.IsActive,
            departmentId = machine.DepartmentId,
            syncId = machine.SyncId,
            capacity = 2.5m,
            efficiencyFactor = 0.85m
        });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert
        var fetched = await ReadAsync<MachineDto>(await second.GetAsync($"{MachinesUrl}/{machine.Id}"));
        fetched.CreatedBy.Should().Be(firstAdminId);
        fetched.ModifiedBy.Should().Be(secondUserId);
        fetched.UpdatedAt.Should().NotBeNull();

        var browsed = await ReadAsync<PagedResponseDto<MachineDto>>(
            await second.GetAsync($"{MachinesUrl}?code={machine.Code}"));
        browsed.Items.Should().ContainSingle(m =>
            m.Id == machine.Id && m.CreatedBy == firstAdminId && m.ModifiedBy == secondUserId);
    }

    [Fact]
    public async Task AnonymousTenantCreate_Succeeds()
    {
        // Arrange — pre-authentication bootstrap write has no authenticated
        // caller, so actors persist as null; the write must not fail validation.
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/tenants", new
        {
            name = $"audit-{suffix}",
            displayName = $"Audit Tenant {suffix}",
            contactEmail = $"audit-{suffix}@integration.local",
            settings = string.Empty,
            password = "Passw0rd!",
            confirmPassword = "Passw0rd!"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var tenant = await ReadAsync<AnonymousTenantDto>(response);
        tenant.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CrossTenant_ReadsReturnEmpty_NoActorLeak()
    {
        // Arrange — order lives in tenant A.
        var (emailA, passwordA) = await Fixture.CreateTenantAsync();
        using var clientA = await Fixture.CreateAuthenticatedClientAsync(emailA, passwordA);
        var order = await CreateOrderAsync(clientA);

        var (emailB, passwordB) = await Fixture.CreateTenantAsync();
        using var clientB = await Fixture.CreateAuthenticatedClientAsync(emailB, passwordB);

        // Act — tenant B reads by id and by browse filter.
        var get = await clientB.GetAsync($"{OrdersUrl}/{order.Id}");
        var browse = await clientB.GetAsync($"{OrdersUrl}?code={order.Code}");

        // Assert
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);

        browse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<ProductionOrderDto>>(browse);
        page.Items.Should().BeEmpty();
        page.Items.Should().NotContain(o => o.CreatedBy == order.CreatedBy && order.CreatedBy != null);
    }

    private async Task<ProductionOrderDto> CreateOrderAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(OrdersUrl, new
        {
            code = $"PO-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            productId = Guid.NewGuid(),
            recipeId = Guid.NewGuid(),
            recipeVersionId = Guid.NewGuid(),
            plannedQuantity = 100m,
            measureUnitId = (Guid?)null,
            priority = 0,
            dueDate = (DateTime?)null,
            notes = (string?)null,
            syncId = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<ProductionOrderDto>(response);
    }

    private async Task<MachineDto> CreateMachineAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(MachinesUrl, new
        {
            code = $"IT-{Guid.NewGuid():N}"[..12],
            name = "Work Center",
            description = (string?)null,
            isActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<MachineDto>(response);
    }

    private async Task<string> SignInAsync(string email, string password)
    {
        using var client = Fixture.CreateClient();
        var response = await client.PostAsJsonAsync(SignInUrl, new { email, password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = await ReadAsync<SignInResponse>(response);
        return token.AccessToken;
    }

    private HttpClient ClientWithToken(string accessToken)
    {
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
        var email = $"audituser-{suffix}@integration.local";
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

    private static Guid DecodeSub(string accessToken)
    {
        var payload = accessToken.Split('.')[1]
            .Replace('-', '+')
            .Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        using var document = JsonDocument.Parse(json);

        var sub = document.RootElement.TryGetProperty("sub", out var subProp)
            ? subProp.GetString()
            : document.RootElement.TryGetProperty("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", out var ni)
                ? ni.GetString()
                : null;

        Guid.TryParse(sub, out var userId).Should().BeTrue("JWT must carry the caller user id in sub");
        return userId;
    }

    private sealed record SignInResponse(string AccessToken);
}
