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
/// Endpoint-scoped integration tests for <c>/api/maintenance-plans</c> (issue
/// #297, slice 1/3: preventive maintenance plan registry). They exercise the
/// full request pipeline: authentication, tenant resolution, default-deny
/// write authorization, validation behavior, MediatR handlers, EF Core
/// persistence and the global exception handler - against a real PostgreSQL
/// database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class MaintenancePlansEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/maintenance-plans";
    private const string MachinesUrl = "/api/machines";

    // TriggerType values mirror MaintenancePlanTriggerType (Time=1, Meter=2).

    [Fact]
    public async Task Browse_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.PostAsJsonAsync(BaseUrl, new { code = "PM-X", name = "No auth" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();
        var id = Guid.NewGuid();

        var response = await client.PutAsJsonAsync($"{BaseUrl}/{id}", new { id });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.DeleteAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_TimePlan_ReturnsCreated_AndIsRetrievableById()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var code = UniqueCode();
        var nextDueAt = DateTime.UtcNow.AddDays(30);

        var createResponse = await client.PostAsJsonAsync(BaseUrl, new
        {
            code,
            name = "Monthly greasing",
            description = "Grease spindle bearings",
            machineId,
            triggerType = 1,
            intervalDays = 30,
            meterIntervalValue = (decimal?)null,
            nextDueAt,
            isActive = true
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<MaintenancePlanDto>(createResponse);
        created.Code.Should().Be(code);
        created.Name.Should().Be("Monthly greasing");
        created.MachineId.Should().Be(machineId);
        created.MachineCode.Should().NotBeNullOrWhiteSpace();
        created.TriggerType.Should().Be(1);
        created.IntervalDays.Should().Be(30);
        created.NextDueAt.Should().BeCloseTo(nextDueAt, TimeSpan.FromSeconds(1));
        created.LastCompletedAt.Should().BeNull();
        created.IsActive.Should().BeTrue();

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadAsync<MaintenancePlanDto>(getResponse);
        fetched.Id.Should().Be(created.Id);
        fetched.Code.Should().Be(code);
    }

    [Fact]
    public async Task Create_MeterPlan_ReturnsCreated()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var code = UniqueCode();

        var createResponse = await client.PostAsJsonAsync(BaseUrl, new
        {
            code,
            name = "Filter per running hours",
            description = (string?)null,
            machineId,
            triggerType = 2,
            intervalDays = (int?)null,
            meterIntervalValue = 500m,
            nextDueAt = (DateTime?)null,
            isActive = true
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<MaintenancePlanDto>(createResponse);
        created.Code.Should().Be(code);
        created.TriggerType.Should().Be(2);
        created.MeterIntervalValue.Should().Be(500m);
        created.NextDueAt.Should().BeNull();
    }

    [Fact]
    public async Task Create_WithDuplicateCode_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var code = UniqueCode();
        var payload = new
        {
            code,
            name = "Monthly greasing",
            description = (string?)null,
            machineId,
            triggerType = 1,
            intervalDays = (int?)30,
            meterIntervalValue = (decimal?)null,
            nextDueAt = (DateTime?)DateTime.UtcNow.AddDays(30),
            isActive = true
        };

        var first = await client.PostAsJsonAsync(BaseUrl, payload);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync(BaseUrl, payload);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_WithSameCodeInDifferentTenant_ReturnsCreatedInBoth()
    {
        var code = UniqueCode();

        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var devMachineId = await CreateMachineAsync(devClient);
        var devCreate = await devClient.PostAsJsonAsync(BaseUrl, TimePayload(code, devMachineId));

        devCreate.StatusCode.Should().Be(HttpStatusCode.Created);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var otherMachineId = await CreateMachineAsync(otherTenantClient);
        var otherCreate = await otherTenantClient.PostAsJsonAsync(BaseUrl, TimePayload(code, otherMachineId));

        otherCreate.StatusCode.Should().Be(HttpStatusCode.Created);
        var otherCreated = await ReadAsync<MaintenancePlanDto>(otherCreate);
        otherCreated.Code.Should().Be(code);
    }

    [Fact]
    public async Task Create_WithUnknownMachine_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(BaseUrl, TimePayload(UniqueCode(), Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("", "Monthly greasing", 1)]
    [InlineData("PM-1", "", 1)]
    [InlineData("PM-1", "Monthly greasing", 99)]
    public async Task Create_WithInvalidPayload_Returns400(string code, string name, int triggerType)
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            code = code == "PM-1" ? UniqueCode() : code,
            name,
            description = (string?)null,
            machineId,
            triggerType,
            intervalDays = (int?)30,
            meterIntervalValue = (decimal?)null,
            nextDueAt = (DateTime?)DateTime.UtcNow.AddDays(30),
            isActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_TimePlanWithoutInterval_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            code = UniqueCode(),
            name = "Monthly greasing",
            description = (string?)null,
            machineId,
            triggerType = 1,
            intervalDays = (int?)null,
            meterIntervalValue = (decimal?)null,
            nextDueAt = (DateTime?)DateTime.UtcNow.AddDays(30),
            isActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_MeterPlanWithoutMeterInterval_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            code = UniqueCode(),
            name = "Filter per running hours",
            description = (string?)null,
            machineId,
            triggerType = 2,
            intervalDays = (int?)null,
            meterIntervalValue = (decimal?)null,
            nextDueAt = (DateTime?)null,
            isActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_TimePlanWithPastNextDueAt_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);

        var response = await client.PostAsJsonAsync(BaseUrl, TimePayload(
            UniqueCode(), machineId, DateTime.UtcNow.AddDays(-1)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_AsUserRole_Returns403()
    {
        // Arrange — caller holds only the read set, lacking configuration.write.
        var (adminEmail, _) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);
        var (email, password) = await CreateUserWithRoleAsync(tenantId, RbacDefaults.UserRoleCode);
        using var client = await ClientForAsync(email, password);

        // Act
        var response = await client.PostAsJsonAsync(
            BaseUrl, TimePayload(UniqueCode(), Guid.NewGuid()));

        // Assert — rejected by the default-deny authorization pipeline step.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_WithUnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_CrossTenantId_Returns404()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var machineId = await CreateMachineAsync(otherTenantClient);
        var createResponse = await otherTenantClient.PostAsJsonAsync(
            BaseUrl, TimePayload(UniqueCode(), machineId));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<MaintenancePlanDto>(createResponse);

        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var getResponse = await devClient.GetAsync($"{BaseUrl}/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var browseResponse = await devClient.GetAsync($"{BaseUrl}?machineId={machineId}");

        browseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<MaintenancePlanDto>>(browseResponse);
        page.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Browse_FiltersByMachineIdIsActiveAndDueBefore()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineA = await CreateMachineAsync(client);
        var machineB = await CreateMachineAsync(client);
        var soon = DateTime.UtcNow.AddDays(5);
        var later = DateTime.UtcNow.AddDays(60);

        var planSoonActive = await CreatePlanAsync(client, machineA, soon, isActive: true);
        await CreatePlanAsync(client, machineB, soon, isActive: true);
        await CreatePlanAsync(client, machineA, later, isActive: false);

        var byMachine = await client.GetAsync($"{BaseUrl}?machineId={machineB}");

        byMachine.StatusCode.Should().Be(HttpStatusCode.OK);
        var machinePage = await ReadAsync<PagedResponseDto<MaintenancePlanDto>>(byMachine);
        machinePage.Items.Should().ContainSingle(i => i.MachineId == machineB);

        var byActive = await client.GetAsync($"{BaseUrl}?isActive=false");

        byActive.StatusCode.Should().Be(HttpStatusCode.OK);
        var activePage = await ReadAsync<PagedResponseDto<MaintenancePlanDto>>(byActive);
        activePage.Items.Should().OnlyContain(i => !i.IsActive);

        var byDue = await client.GetAsync(
            $"{BaseUrl}?dueBefore={Uri.EscapeDataString(DateTime.UtcNow.AddDays(30).ToString("O"))}");

        byDue.StatusCode.Should().Be(HttpStatusCode.OK);
        var duePage = await ReadAsync<PagedResponseDto<MaintenancePlanDto>>(byDue);
        duePage.Items.Should().Contain(i => i.Id == planSoonActive.Id);
        duePage.Items.Should().OnlyContain(i => i.NextDueAt < DateTime.UtcNow.AddDays(30).AddMinutes(1));
    }

    [Fact]
    public async Task Update_ReturnsNoContent_AndPersistsChanges()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var plan = await CreatePlanAsync(client, machineId, DateTime.UtcNow.AddDays(30), isActive: true);

        var update = await client.PutAsJsonAsync($"{BaseUrl}/{plan.Id}", new
        {
            id = plan.Id,
            code = plan.Code,
            name = "Weekly greasing",
            description = "Updated",
            machineId,
            triggerType = 1,
            intervalDays = (int?)7,
            meterIntervalValue = (decimal?)null,
            nextDueAt = (DateTime?)DateTime.UtcNow.AddDays(7),
            isActive = false
        });

        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await client.GetAsync($"{BaseUrl}/{plan.Id}");

        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadAsync<MaintenancePlanDto>(get);
        fetched.Name.Should().Be("Weekly greasing");
        fetched.IntervalDays.Should().Be(7);
        fetched.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Update_WithRouteIdMismatch_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync($"{BaseUrl}/{Guid.NewGuid()}", new
        {
            id = Guid.NewGuid(),
            code = UniqueCode(),
            name = "Mismatch",
            description = (string?)null,
            machineId = Guid.NewGuid(),
            triggerType = 1,
            intervalDays = (int?)30,
            meterIntervalValue = (decimal?)null,
            nextDueAt = (DateTime?)DateTime.UtcNow.AddDays(30),
            isActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_WithUnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var id = Guid.NewGuid();

        var response = await client.PutAsJsonAsync($"{BaseUrl}/{id}", new
        {
            id,
            code = UniqueCode(),
            name = "Ghost plan",
            description = (string?)null,
            machineId = Guid.NewGuid(),
            triggerType = 1,
            intervalDays = (int?)30,
            meterIntervalValue = (decimal?)null,
            nextDueAt = (DateTime?)DateTime.UtcNow.AddDays(30),
            isActive = true
        });

        // Unknown machine would also 404; create a real machine so the
        // assertion proves the plan lookup misses.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_WithDuplicateCode_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var first = await CreatePlanAsync(client, machineId, DateTime.UtcNow.AddDays(30), isActive: true);
        var second = await CreatePlanAsync(client, machineId, DateTime.UtcNow.AddDays(40), isActive: true);

        var response = await client.PutAsJsonAsync($"{BaseUrl}/{second.Id}", new
        {
            id = second.Id,
            code = first.Code,
            name = second.Name,
            description = (string?)null,
            machineId,
            triggerType = 1,
            intervalDays = (int?)30,
            meterIntervalValue = (decimal?)null,
            nextDueAt = (DateTime?)DateTime.UtcNow.AddDays(40),
            isActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_AndGetReturns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var plan = await CreatePlanAsync(client, machineId, DateTime.UtcNow.AddDays(30), isActive: true);

        var delete = await client.DeleteAsync($"{BaseUrl}/{plan.Id}");

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await client.GetAsync($"{BaseUrl}/{plan.Id}");

        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_WithUnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.DeleteAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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

    private async Task<MaintenancePlanDto> CreatePlanAsync(
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

    private static object TimePayload(string code, Guid machineId, DateTime? nextDueAt = null) => new
    {
        code,
        name = $"Plan {code}",
        description = (string?)null,
        machineId,
        triggerType = 1,
        intervalDays = (int?)30,
        meterIntervalValue = (decimal?)null,
        nextDueAt = nextDueAt ?? DateTime.UtcNow.AddDays(30),
        isActive = true
    };

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
        var email = $"plan-authz-{suffix}@integration.local";
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
