using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>GET /api/maintenance-plans/due</c>
/// and the completion rollover that closes the preventive loop (issue #299,
/// slice 3/3). They exercise the full request pipeline: authentication,
/// tenant resolution, MediatR handlers, EF Core persistence and the global
/// exception handler - against a real PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class MaintenancePlansDueEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string PlansUrl = "/api/maintenance-plans";
    private const string OrdersUrl = "/api/maintenance-work-orders";
    private const string MachinesUrl = "/api/machines";

    [Fact]
    public async Task Due_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync($"{PlansUrl}/due");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Due_ListsScheduledPlansEarliestFirst_WithBadgeFlags()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var soon = await CreateTimePlanAsync(client, machineId, DateTime.UtcNow.AddDays(5));
        var later = await CreateTimePlanAsync(client, machineId, DateTime.UtcNow.AddDays(60));

        var response = await client.GetAsync($"{PlansUrl}/due");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var due = await ReadAsync<List<MaintenancePlanDto>>(response);
        due.Should().Contain(p => p.Id == soon.Id);
        due.Should().Contain(p => p.Id == later.Id);
        // Overdue first: the sooner due date sorts before the later one even
        // with plans from other tests sharing the database.
        var ids = due.Select(p => p.Id).ToList();
        ids.IndexOf(soon.Id).Should().BeLessThan(ids.IndexOf(later.Id));
        // Freshly created future plans are not overdue and carry a day count.
        var soonRow = due.Single(p => p.Id == soon.Id);
        soonRow.IsOverdue.Should().BeFalse();
        soonRow.DueInDays.Should().NotBeNull();
        soonRow.DueInDays!.Value.Should().BeInRange(3, 5);
    }

    [Fact]
    public async Task Due_OverdueOnly_ReturnsEmptyWhenNothingIsPastDue()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var future = await CreateTimePlanAsync(client, machineId, DateTime.UtcNow.AddDays(5));

        var response = await client.GetAsync($"{PlansUrl}/due?overdueOnly=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var due = await ReadAsync<List<MaintenancePlanDto>>(response);
        // The future plan must be excluded; every row that is returned (the
        // database is shared with other tests, so overdue plans from other
        // tests may appear) must actually be overdue. A manual loop is used
        // instead of OnlyContain because FluentAssertions fails OnlyContain
        // on an empty collection, while empty is the expected result here.
        due.Should().NotContain(p => p.Id == future.Id);
        foreach (var row in due)
        {
            row.IsOverdue.Should().BeTrue();
        }
    }

    [Fact]
    public async Task Due_WithInvalidDueWithinDays_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{PlansUrl}/due?dueWithinDays=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Browse_IncludesDueBadgeFields()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var plan = await CreateTimePlanAsync(client, machineId, DateTime.UtcNow.AddDays(30));

        var response = await client.GetAsync($"{PlansUrl}?machineId={machineId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<MaintenancePlanDto>>(response);
        var row = page.Items.Single(i => i.Id == plan.Id);
        row.IsOverdue.Should().BeFalse();
        row.DueInDays.Should().NotBeNull();
        row.DueInDays!.Value.Should().BeInRange(28, 30);
        row.LastCompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task Complete_PlanLinkedOrder_RollsPlanForward()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var plan = await CreateTimePlanAsync(client, machineId, DateTime.UtcNow.AddDays(30));
        var before = await client.GetAsync($"{PlansUrl}/{plan.Id}");
        var beforePlan = await ReadAsync<MaintenancePlanDto>(before);

        var raise = await client.PostAsync($"{PlansUrl}/{plan.Id}/raise-now", null);
        raise.StatusCode.Should().Be(HttpStatusCode.OK);
        var raised = await ReadAsync<MaintenanceWorkOrderDto>(raise);
        raised.PlanId.Should().Be(plan.Id);

        var complete = await client.PostAsJsonAsync(
            $"{OrdersUrl}/{raised.Id}/complete", new { resolutionNotes = "Greased bearings" });

        complete.StatusCode.Should().Be(HttpStatusCode.OK);

        var get = await client.GetAsync($"{PlansUrl}/{plan.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var rolled = await ReadAsync<MaintenancePlanDto>(get);
        rolled.LastCompletedAt.Should().NotBeNull();
        rolled.LastCompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
        // Rolled forward by IntervalDays from the completion instant.
        rolled.NextDueAt.Should().NotBeNull();
        rolled.NextDueAt!.Value.Should().BeCloseTo(
            rolled.LastCompletedAt!.Value.AddDays(30), TimeSpan.FromMinutes(5));
        rolled.NextDueAt.Should().BeOnOrAfter(beforePlan.NextDueAt!.Value);
        rolled.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public async Task Complete_PlanLinkedMeterOrder_AdvancesLastCompletedOnly()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var plan = await CreateMeterPlanAsync(client, machineId);

        var raise = await client.PostAsync($"{PlansUrl}/{plan.Id}/raise-now", null);
        raise.StatusCode.Should().Be(HttpStatusCode.OK);
        var raised = await ReadAsync<MaintenanceWorkOrderDto>(raise);

        var complete = await client.PostAsJsonAsync(
            $"{OrdersUrl}/{raised.Id}/complete", new { resolutionNotes = "Replaced filter" });

        complete.StatusCode.Should().Be(HttpStatusCode.OK);

        var get = await client.GetAsync($"{PlansUrl}/{plan.Id}");
        var rolled = await ReadAsync<MaintenancePlanDto>(get);
        rolled.LastCompletedAt.Should().NotBeNull();
        rolled.NextDueAt.Should().BeNull();
    }

    [Fact]
    public async Task Complete_StandaloneOrder_LeavesPlansUntouched()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var plan = await CreateTimePlanAsync(client, machineId, DateTime.UtcNow.AddDays(30));

        var create = await client.PostAsJsonAsync(OrdersUrl, new
        {
            code = UniqueCode("WO"),
            title = "Standalone breakdown",
            description = (string?)null,
            machineId,
            priority = 2
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await ReadAsync<MaintenanceWorkOrderDto>(create);

        var complete = await client.PostAsJsonAsync(
            $"{OrdersUrl}/{order.Id}/complete", new { resolutionNotes = "Fixed breakdown" });

        complete.StatusCode.Should().Be(HttpStatusCode.OK);

        var get = await client.GetAsync($"{PlansUrl}/{plan.Id}");
        var untouched = await ReadAsync<MaintenancePlanDto>(get);
        untouched.LastCompletedAt.Should().BeNull();
        untouched.NextDueAt.Should().BeCloseTo(plan.NextDueAt!.Value, TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task RaiseNow_CrossTenantPlanId_Returns404_AndLeaksNothing()
    {
        // Arrange — the plan lives in another tenant.
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var otherMachineId = await CreateMachineAsync(otherTenantClient);
        var otherPlan = await CreateTimePlanAsync(otherTenantClient, otherMachineId, DateTime.UtcNow.AddDays(5));

        using var client = await Fixture.CreateAuthenticatedClientAsync();

        // Act — raising from the other tenant's plan must not leak existence.
        var raise = await client.PostAsync($"{PlansUrl}/{otherPlan.Id}/raise-now", null);

        // Assert — hidden by the global tenant query filter, hence unknown.
        raise.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // The other tenant's plan never appears in this tenant's due list.
        var dueResponse = await client.GetAsync($"{PlansUrl}/due");
        dueResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var due = await ReadAsync<List<MaintenancePlanDto>>(dueResponse);
        due.Should().NotContain(p => p.Id == otherPlan.Id);
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
        HttpClient client, Guid machineId, DateTime nextDueAt)
    {
        var response = await client.PostAsJsonAsync(PlansUrl, new
        {
            code = UniqueCode("PM"),
            name = $"Plan {Guid.NewGuid():N}"[..12],
            description = (string?)null,
            machineId,
            triggerType = 1,
            intervalDays = (int?)30,
            meterIntervalValue = (decimal?)null,
            nextDueAt = (DateTime?)nextDueAt,
            isActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<MaintenancePlanDto>(response);
    }

    private async Task<MaintenancePlanDto> CreateMeterPlanAsync(HttpClient client, Guid machineId)
    {
        var response = await client.PostAsJsonAsync(PlansUrl, new
        {
            code = UniqueCode("PM"),
            name = $"Meter plan {Guid.NewGuid():N}"[..12],
            description = (string?)null,
            machineId,
            triggerType = 2,
            intervalDays = (int?)null,
            meterIntervalValue = (decimal?)500m,
            nextDueAt = (DateTime?)null,
            isActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<MaintenancePlanDto>(response);
    }

    private static string UniqueCode(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..12];
}
