using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;
using AsistOff.MES.Multitenancy.Context;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Rbac;
using AsistOff.MES.Users.Core.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for issue #372 (sensitive read
/// projections). A caller holding only the read-only <c>user</c> role
/// (<c>production.read</c> without <c>production.write</c>) browses audit
/// history and OPC UA reads over HTTP 200 with <c>Payload</c> and
/// <c>LastError</c> redacted to null, while the tenant admin on the same
/// filters still sees the full values; liveness flags and counts are
/// identical for both roles. Unauthenticated reads stay 401 and tenant
/// isolation still rides on the global query filter (no bypass added).
/// NB: written without a local run — the CI backend job executes this file
/// against Testcontainers PostgreSQL.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class SensitiveProjectionEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string OrdersUrl = "/api/production-orders";
    private const string MachinesUrl = "/api/machines";
    private const string HistoryUrl = "/api/audit-events";
    private const string ConnectionsUrl = "/api/opcua-connections";

    [Fact]
    public async Task AuditBrowse_ReadOnlyCaller_GetsNullPayload_WhileAdminKeepsFullPayload()
    {
        // Arrange — an order updated with a distinctive note so the payload is non-empty.
        var (adminEmail, adminPassword) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);
        using var admin = await Fixture.CreateAuthenticatedClientAsync(adminEmail, adminPassword);
        var order = await CreateOrderAsync(admin);
        var probe = $"redaction-probe-{Guid.NewGuid():N}"[..24];
        await UpdateOrderNotesAsync(admin, order, probe);

        var (userEmail, userPassword) = await CreateUserWithRoleAsync(tenantId, RbacDefaults.UserRoleCode);
        using var reader = await Fixture.CreateAuthenticatedClientAsync(userEmail, userPassword);
        var filter = $"{HistoryUrl}?entityName=ProductionOrder&entityId={order.Id}";

        // Act
        var adminBrowse = await admin.GetAsync(filter);
        var readerBrowse = await reader.GetAsync(filter);

        // Assert — same rows, redacted versus full projection.
        adminBrowse.StatusCode.Should().Be(HttpStatusCode.OK);
        readerBrowse.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminPage = await ReadAsync<PagedResponseDto<AuditEventDto>>(adminBrowse);
        var readerPage = await ReadAsync<PagedResponseDto<AuditEventDto>>(readerBrowse);
        readerPage.Items.Select(x => x.Id).Should().BeEquivalentTo(adminPage.Items.Select(x => x.Id));

        var updatedByAdmin = adminPage.Items.Should().ContainSingle(x => x.Action == 1).Subject;
        updatedByAdmin.Payload.Should().Contain(probe);

        readerPage.Items.Should().HaveCount(adminPage.Items.Count);
        readerPage.Items.Should().OnlyContain(x => x.Payload == null);
    }

    [Fact]
    public async Task OrderHistory_ReadOnlyCaller_GetsNullPayload()
    {
        // Arrange
        var (adminEmail, adminPassword) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);
        using var admin = await Fixture.CreateAuthenticatedClientAsync(adminEmail, adminPassword);
        var order = await CreateOrderAsync(admin);

        var (userEmail, userPassword) = await CreateUserWithRoleAsync(tenantId, RbacDefaults.UserRoleCode);
        using var reader = await Fixture.CreateAuthenticatedClientAsync(userEmail, userPassword);

        // Act — the per-order history convenience endpoint shares the audit browse handler.
        var response = await reader.GetAsync($"{OrdersUrl}/{order.Id}/history");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<AuditEventDto>>(response);
        page.Items.Should().NotBeEmpty();
        page.Items.Should().OnlyContain(x => x.Payload == null);
    }

    [Fact]
    public async Task OpcuaBrowse_ReadOnlyCaller_GetsNullLastError_WhileAdminKeepsRawMessage()
    {
        // Arrange — a connection carrying a raw provider failure (seeded as the poller would).
        var (adminEmail, adminPassword) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);
        using var admin = await Fixture.CreateAuthenticatedClientAsync(adminEmail, adminPassword);
        var machineId = await CreateMachineAsync(admin);
        var connection = await CreateConnectionAsync(admin, machineId);
        const string rawError = "simulated poller failure: endpoint refused by 10.0.0.9:4840";
        await SetConnectionLastErrorAsync(tenantId, connection.Id, rawError);

        var (userEmail, userPassword) = await CreateUserWithRoleAsync(tenantId, RbacDefaults.UserRoleCode);
        using var reader = await Fixture.CreateAuthenticatedClientAsync(userEmail, userPassword);
        var filter = $"{ConnectionsUrl}?machineId={machineId}";

        // Act
        var adminBrowse = await admin.GetAsync(filter);
        var readerBrowse = await reader.GetAsync(filter);

        // Assert
        adminBrowse.StatusCode.Should().Be(HttpStatusCode.OK);
        readerBrowse.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminRow = (await ReadAsync<PagedResponseDto<OpcUaConnectionDto>>(adminBrowse))
            .Items.Should().ContainSingle(x => x.Id == connection.Id).Subject;
        adminRow.LastError.Should().Be(rawError);

        var readerRow = (await ReadAsync<PagedResponseDto<OpcUaConnectionDto>>(readerBrowse))
            .Items.Should().ContainSingle(x => x.Id == connection.Id).Subject;
        readerRow.LastError.Should().BeNull();
        readerRow.EndpointUrl.Should().Be(adminRow.EndpointUrl);
        readerRow.IsEnabled.Should().Be(adminRow.IsEnabled);
        readerRow.LastSeenAtUtc.Should().Be(adminRow.LastSeenAtUtc);
    }

    [Fact]
    public async Task OpcuaGet_ReadOnlyCaller_GetsNullLastError_WhileAdminKeepsRawMessage()
    {
        // Arrange
        var (adminEmail, adminPassword) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);
        using var admin = await Fixture.CreateAuthenticatedClientAsync(adminEmail, adminPassword);
        var connection = await CreateConnectionAsync(admin);
        const string rawError = "simulated poller failure: BadConnectionClosed";
        await SetConnectionLastErrorAsync(tenantId, connection.Id, rawError);

        var (userEmail, userPassword) = await CreateUserWithRoleAsync(tenantId, RbacDefaults.UserRoleCode);
        using var reader = await Fixture.CreateAuthenticatedClientAsync(userEmail, userPassword);

        // Act
        var adminGet = await admin.GetAsync($"{ConnectionsUrl}/{connection.Id}");
        var readerGet = await reader.GetAsync($"{ConnectionsUrl}/{connection.Id}");

        // Assert
        adminGet.StatusCode.Should().Be(HttpStatusCode.OK);
        readerGet.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<OpcUaConnectionDto>(adminGet)).LastError.Should().Be(rawError);

        var readerRow = await ReadAsync<OpcUaConnectionDto>(readerGet);
        readerRow.LastError.Should().BeNull();
        readerRow.Id.Should().Be(connection.Id);
    }

    [Fact]
    public async Task OpcuaStatus_ReadOnlyCaller_GetsNullLastError_WithUnchangedLivenessAndCounts()
    {
        // Arrange
        var (adminEmail, adminPassword) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);
        using var admin = await Fixture.CreateAuthenticatedClientAsync(adminEmail, adminPassword);
        var machineId = await CreateMachineAsync(admin);
        var connection = await CreateConnectionAsync(admin, machineId);
        const string rawError = "simulated poller failure: BadTimeout";
        await SetConnectionLastErrorAsync(tenantId, connection.Id, rawError);

        var (userEmail, userPassword) = await CreateUserWithRoleAsync(tenantId, RbacDefaults.UserRoleCode);
        using var reader = await Fixture.CreateAuthenticatedClientAsync(userEmail, userPassword);
        var filter = $"{ConnectionsUrl}/status?machineId={machineId}";

        // Act
        var adminStatus = await admin.GetAsync(filter);
        var readerStatus = await reader.GetAsync(filter);

        // Assert — error text redacted, health signal identical.
        adminStatus.StatusCode.Should().Be(HttpStatusCode.OK);
        readerStatus.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminDto = await ReadAsync<OpcUaConnectionStatusDto>(adminStatus);
        var readerDto = await ReadAsync<OpcUaConnectionStatusDto>(readerStatus);

        adminDto.Connections.Should().ContainSingle(e => e.ConnectionId == connection.Id)
            .Which.LastError.Should().Be(rawError);

        var readerEntry = readerDto.Connections.Should()
            .ContainSingle(e => e.ConnectionId == connection.Id).Subject;
        readerEntry.LastError.Should().BeNull();
        readerEntry.IsLive.Should().Be(
            adminDto.Connections.Single(e => e.ConnectionId == connection.Id).IsLive);
        readerDto.TotalCount.Should().Be(adminDto.TotalCount);
        readerDto.LiveCount.Should().Be(adminDto.LiveCount);
        readerDto.StaleCount.Should().Be(adminDto.StaleCount);
        readerDto.DisabledCount.Should().Be(adminDto.DisabledCount);
    }

    [Fact]
    public async Task OpcuaGet_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync($"{ConnectionsUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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

    private static async Task UpdateOrderNotesAsync(HttpClient client, ProductionOrderDto order, string notes)
    {
        var update = await client.PutAsJsonAsync($"{OrdersUrl}/{order.Id}", new
        {
            id = order.Id,
            code = order.Code,
            productId = order.ProductId,
            recipeId = order.RecipeId,
            recipeVersionId = order.RecipeVersionId,
            plannedQuantity = order.PlannedQuantity,
            measureUnitId = (Guid?)null,
            priority = order.Priority,
            dueDate = (DateTime?)null,
            notes,
            syncId = (string?)null,
            concurrencyToken = order.ConcurrencyToken
        });

        update.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<Guid> CreateMachineAsync(HttpClient client)
    {
        var code = $"RP-M-{Guid.NewGuid():N}"[..12];
        var response = await client.PostAsJsonAsync(MachinesUrl, new
        {
            code,
            name = code,
            description = (string?)null,
            isActive = true
        });

        response.EnsureSuccessStatusCode();
        return (await ReadAsync<MachineDto>(response)).Id;
    }

    private static async Task<OpcUaConnectionDto> CreateConnectionAsync(HttpClient client, Guid? machineId = null)
    {
        machineId ??= await CreateMachineAsync(client);
        var response = await client.PostAsJsonAsync(ConnectionsUrl, new
        {
            machineId,
            endpointUrl = $"opc.tcp://plc-{Guid.NewGuid():N}.local:4840",
            securityPolicy = 1,
            pollIntervalSeconds = 30
        });

        response.EnsureSuccessStatusCode();
        return await ReadAsync<OpcUaConnectionDto>(response);
    }

    private async Task SetConnectionLastErrorAsync(Guid tenantId, Guid connectionId, string lastError)
    {
        // The background poller is disabled in the integration host, so the
        // raw provider message is seeded the way the poller would write it.
        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
            var entity = await context.Set<OpcUaConnection>()
                .SingleAsync(x => x.Id == connectionId);
            entity.LastError = lastError;
            await context.SaveChangesAsync();
        }
    }

    private async Task<(string Email, string Password)> CreateUserWithRoleAsync(
        Guid tenantId, string roleCode)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"redact-{suffix}@integration.local";
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
}
