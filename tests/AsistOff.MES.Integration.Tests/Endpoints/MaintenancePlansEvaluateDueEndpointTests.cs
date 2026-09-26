using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AsistOff.MES.Configuration.Domain.Entities;
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
/// Endpoint-scoped integration tests for the slice 2/3 due evaluation
/// (issue #298): <c>POST /api/maintenance-plans/evaluate-due</c> and
/// <c>POST /api/maintenance-plans/{id}/raise-now</c>. They exercise the full
/// request pipeline — authentication, tenant resolution, default-deny write
/// authorization, validation behavior, MediatR handlers, EF Core persistence
/// and the global exception handler — against a real PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class MaintenancePlansEvaluateDueEndpointTests(MesApplicationFixture fixture)
    : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/maintenance-plans";
    private const string MachinesUrl = "/api/machines";
    private const string WorkOrdersUrl = "/api/maintenance-work-orders";

    [Fact]
    public async Task EvaluateDue_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.PostAsJsonAsync($"{BaseUrl}/evaluate-due", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RaiseNow_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.PostAsync($"{BaseUrl}/{Guid.NewGuid()}/raise-now", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EvaluateDue_AsUserRole_Returns403()
    {
        var (adminEmail, _) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);
        var (email, password) = await CreateUserWithRoleAsync(tenantId, RbacDefaults.UserRoleCode);
        using var client = await ClientForAsync(email, password);

        var response = await client.PostAsJsonAsync($"{BaseUrl}/evaluate-due", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RaiseNow_AsUserRole_Returns403()
    {
        var (adminEmail, _) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);
        var (email, password) = await CreateUserWithRoleAsync(tenantId, RbacDefaults.UserRoleCode);
        using var client = await ClientForAsync(email, password);

        var response = await client.PostAsync($"{BaseUrl}/{Guid.NewGuid()}/raise-now", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EvaluateDue_CreatesWorkOrderForDueTimePlan_AndSkipsNotYetDue()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var duePlan = await CreateTimePlanAsync(client, machineId, DateTime.UtcNow.AddDays(30), isActive: true);
        var futurePlan = await CreateTimePlanAsync(client, machineId, DateTime.UtcNow.AddDays(90), isActive: true);
        await BackdatePlanAsync(duePlan.Id, IntegrationTestData.AdminEmail, DateTime.UtcNow.AddHours(-1));

        var response = await client.PostAsJsonAsync($"{BaseUrl}/evaluate-due", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var raised = await ReadAsync<List<MaintenanceWorkOrderDto>>(response);
        raised.Should().ContainSingle(o => o.PlanId == duePlan.Id);
        raised.Should().NotContain(o => o.PlanId == futurePlan.Id);

        var order = raised.Single(o => o.PlanId == duePlan.Id);
        order.MachineId.Should().Be(machineId);
        order.Status.Should().Be(1);

        var get = await client.GetAsync($"{WorkOrdersUrl}/{order.Id}");

        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadAsync<MaintenanceWorkOrderDto>(get);
        fetched.PlanId.Should().Be(duePlan.Id);
        fetched.MachineId.Should().Be(machineId);
    }

    [Fact]
    public async Task EvaluateDue_WithMeterReading_CreatesForDueMeterPlan_AndSkipsOthers()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        // Small thresholds keep this test isolated from leftover meter plans
        // (interval 500) created by sibling test classes in the shared tenant.
        var duePlan = await CreateMeterPlanAsync(client, machineId, 10m);
        var expensivePlan = await CreateMeterPlanAsync(client, machineId, 1000m);

        var response = await client.PostAsJsonAsync(
            $"{BaseUrl}/evaluate-due", new { currentMeterReading = 25m });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var raised = await ReadAsync<List<MaintenanceWorkOrderDto>>(response);
        raised.Should().ContainSingle(o => o.PlanId == duePlan.Id);
        raised.Should().NotContain(o => o.PlanId == expensivePlan.Id);
    }

    [Fact]
    public async Task EvaluateDue_WithPerMachineReadings_CreatesForDueMeterPlan()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var duePlan = await CreateMeterPlanAsync(client, machineId, 10m);

        var response = await client.PostAsJsonAsync(
            $"{BaseUrl}/evaluate-due",
            new { meterReadings = new Dictionary<string, decimal> { [machineId.ToString()] = 50m } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var raised = await ReadAsync<List<MaintenanceWorkOrderDto>>(response);
        raised.Should().Contain(o => o.PlanId == duePlan.Id);
    }

    [Fact]
    public async Task EvaluateDue_WithoutMeterReading_SkipsMeterPlans()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var meterPlan = await CreateMeterPlanAsync(client, machineId, 10m);

        var response = await client.PostAsJsonAsync($"{BaseUrl}/evaluate-due", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var raised = await ReadAsync<List<MaintenanceWorkOrderDto>>(response);
        raised.Should().NotContain(o => o.PlanId == meterPlan.Id);
    }

    [Fact]
    public async Task EvaluateDue_RepeatWithoutCompleting_DoesNotDuplicate()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var duePlan = await CreateTimePlanAsync(client, machineId, DateTime.UtcNow.AddDays(30), isActive: true);
        await BackdatePlanAsync(duePlan.Id, IntegrationTestData.AdminEmail, DateTime.UtcNow.AddHours(-1));

        var first = await client.PostAsJsonAsync($"{BaseUrl}/evaluate-due", new { });
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<List<MaintenanceWorkOrderDto>>(first))
            .Should().ContainSingle(o => o.PlanId == duePlan.Id);

        var second = await client.PostAsJsonAsync($"{BaseUrl}/evaluate-due", new { });
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<List<MaintenanceWorkOrderDto>>(second))
            .Should().NotContain(o => o.PlanId == duePlan.Id);
    }

    [Fact]
    public async Task EvaluateDue_SkipsInactivePlans()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var inactivePlan = await CreateTimePlanAsync(client, machineId, DateTime.UtcNow.AddDays(30), isActive: false);
        await BackdatePlanAsync(inactivePlan.Id, IntegrationTestData.AdminEmail, DateTime.UtcNow.AddHours(-1));

        var response = await client.PostAsJsonAsync($"{BaseUrl}/evaluate-due", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<List<MaintenanceWorkOrderDto>>(response))
            .Should().NotContain(o => o.PlanId == inactivePlan.Id);
    }

    [Fact]
    public async Task RaiseNow_CreatesWorkOrder_AndIsIdempotent()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var plan = await CreateTimePlanAsync(client, machineId, DateTime.UtcNow.AddDays(30), isActive: true);

        var first = await client.PostAsync($"{BaseUrl}/{plan.Id}/raise-now", null);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await ReadAsync<MaintenanceWorkOrderDto>(first);
        created.PlanId.Should().Be(plan.Id);
        created.MachineId.Should().Be(machineId);
        created.Status.Should().Be(1);

        var second = await client.PostAsync($"{BaseUrl}/{plan.Id}/raise-now", null);

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<MaintenanceWorkOrderDto>(second)).Id.Should().Be(created.Id);

        var browse = await client.GetAsync($"{WorkOrdersUrl}?machineId={machineId}");

        browse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<MaintenanceWorkOrderDto>>(browse);
        page.Items.Should().Contain(i => i.Id == created.Id && i.PlanId == plan.Id);
    }

    [Fact]
    public async Task RaiseNow_UnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsync($"{BaseUrl}/{Guid.NewGuid()}/raise-now", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RaiseNow_InactivePlan_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var plan = await CreateTimePlanAsync(client, machineId, DateTime.UtcNow.AddDays(30), isActive: false);

        var response = await client.PostAsync($"{BaseUrl}/{plan.Id}/raise-now", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RaisedWorkOrder_IsInvisibleToOtherTenant()
    {
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(devClient);
        var plan = await CreateTimePlanAsync(devClient, machineId, DateTime.UtcNow.AddDays(30), isActive: true);
        var raise = await devClient.PostAsync($"{BaseUrl}/{plan.Id}/raise-now", null);
        raise.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await ReadAsync<MaintenanceWorkOrderDto>(raise);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var getResponse = await otherTenantClient.GetAsync($"{WorkOrdersUrl}/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<Guid> CreateMachineAsync(HttpClient client)
    {
        var code = $"MC-{Guid.NewGuid():N}"[..12];
        var response = await client.PostAsJsonAsync(MachinesUrl, new
        {
            code,
            name = $"Machine {code}",
            description = (string?)null,
            isActive = true,
            departmentId = (Guid?)null,
            syncId = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var machine = await ReadAsync<MachineDto>(response);
        return machine.Id;
    }

    private async Task<MaintenancePlanDto> CreateTimePlanAsync(
        HttpClient client, Guid machineId, DateTime nextDueAt, bool isActive)
    {
        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            code = UniqueCode(),
            name = $"Plan {Guid.NewGuid():N}"[..12],
            description = (string?)null,
            machineId,
            triggerType = 1,
            intervalDays = (int?)30,
            meterIntervalValue = (decimal?)null,
            nextDueAt = (DateTime?)nextDueAt,
            isActive
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<MaintenancePlanDto>(response);
    }

    private async Task<MaintenancePlanDto> CreateMeterPlanAsync(HttpClient client, Guid machineId, decimal interval)
    {
        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            code = UniqueCode(),
            name = $"Meter plan {Guid.NewGuid():N}"[..12],
            description = (string?)null,
            machineId,
            triggerType = 2,
            intervalDays = (int?)null,
            meterIntervalValue = (decimal?)interval,
            nextDueAt = (DateTime?)null,
            isActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<MaintenancePlanDto>(response);
    }

    /// <summary>
    /// Plan creation rejects a past <c>NextDueAt</c> by design, so tests make a
    /// plan due by backdating it directly in the database (same tenant scope).
    /// </summary>
    private async Task BackdatePlanAsync(Guid planId, string ownerEmail, DateTime nextDueAt)
    {
        var tenantId = await GetTenantIdByEmailAsync(ownerEmail);
        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DefaultContext>();
            var plan = await db.Set<MaintenancePlan>().SingleAsync(x => x.Id == planId);
            plan.NextDueAt = nextDueAt;
            await db.SaveChangesAsync();
        }
    }

    private static string UniqueCode() => $"PM-{Guid.NewGuid():N}"[..12];

    private async Task<string> SignInAsync(string email, string password)
    {
        using var client = Fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new { email, password });

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
        var email = $"plan-due-authz-{suffix}@integration.local";
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
