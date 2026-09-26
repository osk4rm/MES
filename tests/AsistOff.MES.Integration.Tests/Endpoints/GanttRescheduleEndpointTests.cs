using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
/// Endpoint-scoped integration tests for <c>PUT /api/schedule/gantt/segments/{id}</c> -
/// the manual Gantt reschedule and leveling write path (issue #305, slice 2/3).
/// They exercise the full request pipeline (auth, tenant resolution, permission
/// gate, validation, parent-order optimistic concurrency, overlap detection,
/// shift-coverage flag and the global exception handler) against a real
/// PostgreSQL database. The route id is the OperationNodeId; the handler
/// upserts the <c>ScheduledOperation</c> override, so the first move of a
/// computed bar creates the row.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class GanttRescheduleEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/schedule/gantt/segments";
    private const string GanttUrl = "/api/schedule/gantt";

    // A window no other test class seeds into, so bars stay ours.
    private const string From = "2027-05-10";
    private const string To = "2027-05-12";

    private static readonly DateTime DueDate = new(2027, 5, 11, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime MovedStart = new(2027, 5, 11, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime MovedEnd = MovedStart.AddHours(2);

    [Fact]
    public async Task Reschedule_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.PutAsJsonAsync($"{BaseUrl}/{Guid.NewGuid()}", MoveBody(
            Guid.NewGuid(), MovedStart, MovedEnd, Guid.NewGuid(), "7", false, null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Reschedule_AsUserRole_Returns403()
    {
        // Arrange — caller holds only the read set, lacking production.write.
        var (adminEmail, _) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);
        var (email, password) = await CreateUserWithRoleAsync(tenantId, RbacDefaults.UserRoleCode);
        using var client = await ClientForAsync(email, password);

        // Act
        var response = await client.PutAsJsonAsync($"{BaseUrl}/{Guid.NewGuid()}", MoveBody(
            Guid.NewGuid(), MovedStart, MovedEnd, Guid.NewGuid(), "7", false, null));

        // Assert — rejected by the default-deny authorization pipeline step.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reschedule_StartAfterEnd_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = UniqueTag();
        var machine = await CreateMachineAsync(client, $"GNT-{tag}-WC");
        var recipe = await CreateRecipeAsync(client);
        var op = await CreateOperationAsync(client, recipe.Versions.Single().Id, "A", perUnitSeconds: 60m);
        await AddResourceAsync(client, op, machine.Id);
        await ReleaseVersionAsync(client, recipe.Versions.Single().Id);
        var order = await CreateReleasedOrderAsync(client, tag, "BAD", recipe, DueDate);

        var response = await client.PutAsJsonAsync($"{BaseUrl}/{op.Id}", MoveBody(
            order.Id, MovedEnd, MovedStart, machine.Id, order.ConcurrencyToken, false, null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reschedule_HappyPath_PersistsMove_AndGetReflectsIt()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = UniqueTag();
        var machine = await CreateMachineAsync(client, $"GNT-{tag}-WC");
        var recipe = await CreateRecipeAsync(client);
        var op = await CreateOperationAsync(client, recipe.Versions.Single().Id, "A", perUnitSeconds: 60m);
        await AddResourceAsync(client, op, machine.Id);
        await ReleaseVersionAsync(client, recipe.Versions.Single().Id);
        var order = await CreateReleasedOrderAsync(client, tag, "MOV", recipe, DueDate);

        var move = await client.PutAsJsonAsync($"{BaseUrl}/{op.Id}", MoveBody(
            order.Id, MovedStart, MovedEnd, machine.Id, order.ConcurrencyToken, false, null));

        move.StatusCode.Should().Be(HttpStatusCode.OK);
        var moved = await ReadAsync<GanttRescheduleDto>(move);
        moved.ProductionOrderId.Should().Be(order.Id);
        moved.OperationNodeId.Should().Be(op.Id);
        moved.MachineId.Should().Be(machine.Id);
        moved.PlannedStart.Should().Be(MovedStart);
        moved.PlannedEnd.Should().Be(MovedEnd);
        moved.ConflictingSegmentIds.Should().BeEmpty();

        var gantt = await client.GetAsync($"{GanttUrl}?from={From}&to={To}");

        gantt.StatusCode.Should().Be(HttpStatusCode.OK);
        var schedule = await ReadAsync<GanttScheduleDto>(gantt);
        var bar = schedule.Groups.SelectMany(g => g.Bars)
            .Where(b => b.ProductionOrderCode == order.Code)
            .Should().ContainSingle().Subject;
        bar.OperationNodeId.Should().Be(op.Id);
        bar.MachineId.Should().Be(machine.Id);
        bar.PlannedStart.Should().Be(MovedStart);
        bar.PlannedEnd.Should().Be(MovedEnd);
    }

    [Fact]
    public async Task Reschedule_OverlappingMove_Returns409_ListingConflicts_AndPersistsNothing()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = UniqueTag();
        var machine = await CreateMachineAsync(client, $"GNT-{tag}-WC");
        var recipe = await CreateRecipeAsync(client);
        var versionId = recipe.Versions.Single().Id;
        var op = await CreateOperationAsync(client, versionId, "A", perUnitSeconds: 60m);
        await AddResourceAsync(client, op, machine.Id);
        await ReleaseVersionAsync(client, versionId);

        // One operation shared by two orders: 60s/unit x 60 units = 60min,
        // computed at [11:00, 12:00) for both.
        var first = await CreateReleasedOrderAsync(client, tag, "LVL1", recipe, DueDate);
        var second = await CreateReleasedOrderAsync(client, tag, "LVL2", recipe, DueDate);

        var firstMove = await client.PutAsJsonAsync($"{BaseUrl}/{op.Id}", MoveBody(
            first.Id, MovedStart, MovedEnd, machine.Id, first.ConcurrencyToken, false, null));
        firstMove.EnsureSuccessStatusCode();
        var pinned = await ReadAsync<GanttRescheduleDto>(firstMove);

        var conflicting = await client.PutAsJsonAsync($"{BaseUrl}/{op.Id}", MoveBody(
            second.Id, MovedStart.AddHours(1), MovedEnd.AddHours(1), machine.Id,
            second.ConcurrencyToken, false, null));

        conflicting.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var blocking = await ConflictingIdsAsync(conflicting);
        blocking.Should().ContainSingle().Which.Should().Be(pinned.Id);

        // The rejected move persisted nothing: the second order still shows
        // its computed bar, and no bar starts at the rejected window.
        var gantt = await client.GetAsync($"{GanttUrl}?from={From}&to={To}");
        gantt.EnsureSuccessStatusCode();
        var schedule = await ReadAsync<GanttScheduleDto>(gantt);
        var secondBars = schedule.Groups.SelectMany(g => g.Bars)
            .Where(b => b.ProductionOrderCode == second.Code).ToList();
        secondBars.Should().ContainSingle().Which.PlannedStart.Should().Be(DueDate.AddMinutes(-60));
        schedule.Groups.SelectMany(g => g.Bars)
            .Where(b => b.ProductionOrderCode == second.Code)
            .Should().OnlyContain(b => b.PlannedStart != MovedStart.AddHours(1));
    }

    [Fact]
    public async Task Reschedule_OverlappingMove_WithForce_Persists_AndEchoesBypassedIds()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = UniqueTag();
        var machine = await CreateMachineAsync(client, $"GNT-{tag}-WC");
        var recipe = await CreateRecipeAsync(client);
        var versionId = recipe.Versions.Single().Id;
        var op = await CreateOperationAsync(client, versionId, "A", perUnitSeconds: 60m);
        await AddResourceAsync(client, op, machine.Id);
        await ReleaseVersionAsync(client, versionId);
        var first = await CreateReleasedOrderAsync(client, tag, "FRC1", recipe, DueDate);
        var second = await CreateReleasedOrderAsync(client, tag, "FRC2", recipe, DueDate);

        var firstMove = await client.PutAsJsonAsync($"{BaseUrl}/{op.Id}", MoveBody(
            first.Id, MovedStart, MovedEnd, machine.Id, first.ConcurrencyToken, false, null));
        firstMove.EnsureSuccessStatusCode();
        var pinned = await ReadAsync<GanttRescheduleDto>(firstMove);

        var forced = await client.PutAsJsonAsync($"{BaseUrl}/{op.Id}", MoveBody(
            second.Id, MovedStart.AddHours(1), MovedEnd.AddHours(1), machine.Id,
            second.ConcurrencyToken, true, null));

        forced.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await ReadAsync<GanttRescheduleDto>(forced);
        result.ConflictingSegmentIds.Should().ContainSingle().Which.Should().Be(pinned.Id);
        result.PlannedStart.Should().Be(MovedStart.AddHours(1));
    }

    [Fact]
    public async Task Reschedule_StaleToken_Returns409_WithRetryGuidance()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = UniqueTag();
        var machine = await CreateMachineAsync(client, $"GNT-{tag}-WC");
        var recipe = await CreateRecipeAsync(client);
        var op = await CreateOperationAsync(client, recipe.Versions.Single().Id, "A", perUnitSeconds: 60m);
        await AddResourceAsync(client, op, machine.Id);
        await ReleaseVersionAsync(client, recipe.Versions.Single().Id);
        var order = await CreateReleasedOrderAsync(client, tag, "STL", recipe, DueDate);

        var response = await client.PutAsJsonAsync($"{BaseUrl}/{op.Id}", MoveBody(
            order.Id, MovedStart, MovedEnd, machine.Id, "0", false, null));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("concurrencyToken");
    }

    [Fact]
    public async Task Reschedule_UnknownMachine_Returns404_AndPersistsNothing()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = UniqueTag();
        var machine = await CreateMachineAsync(client, $"GNT-{tag}-WC");
        var recipe = await CreateRecipeAsync(client);
        var op = await CreateOperationAsync(client, recipe.Versions.Single().Id, "A", perUnitSeconds: 60m);
        await AddResourceAsync(client, op, machine.Id);
        await ReleaseVersionAsync(client, recipe.Versions.Single().Id);
        var order = await CreateReleasedOrderAsync(client, tag, "UNK", recipe, DueDate);

        var response = await client.PutAsJsonAsync($"{BaseUrl}/{op.Id}", MoveBody(
            order.Id, MovedStart, MovedEnd, Guid.NewGuid(), order.ConcurrencyToken, false, null));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Nothing persisted: the computed bar (60min ending at the due date) is intact.
        var gantt = await client.GetAsync($"{GanttUrl}?from={From}&to={To}");
        gantt.EnsureSuccessStatusCode();
        var schedule = await ReadAsync<GanttScheduleDto>(gantt);
        schedule.Groups.SelectMany(g => g.Bars)
            .Where(b => b.ProductionOrderCode == order.Code)
            .Should().ContainSingle().Which.PlannedStart.Should().Be(DueDate.AddMinutes(-60));
    }

    [Fact]
    public async Task Reschedule_CrossTenantMachine_Returns404_AndPersistsNothing()
    {
        var (foreignEmail, foreignPassword) = await Fixture.CreateTenantAsync();
        using var foreignClient = await Fixture.CreateAuthenticatedClientAsync(foreignEmail, foreignPassword);
        var foreignMachine = await CreateMachineAsync(foreignClient, $"GNT-{UniqueTag()}-FRN");

        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = UniqueTag();
        var machine = await CreateMachineAsync(client, $"GNT-{tag}-WC");
        var recipe = await CreateRecipeAsync(client);
        var op = await CreateOperationAsync(client, recipe.Versions.Single().Id, "A", perUnitSeconds: 60m);
        await AddResourceAsync(client, op, machine.Id);
        await ReleaseVersionAsync(client, recipe.Versions.Single().Id);
        var order = await CreateReleasedOrderAsync(client, tag, "XTM", recipe, DueDate);

        var response = await client.PutAsJsonAsync($"{BaseUrl}/{op.Id}", MoveBody(
            order.Id, MovedStart, MovedEnd, foreignMachine.Id, order.ConcurrencyToken, false, null));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Nothing persisted: the computed bar is intact and the foreign lane never appears.
        var gantt = await client.GetAsync($"{GanttUrl}?from={From}&to={To}");
        gantt.EnsureSuccessStatusCode();
        var schedule = await ReadAsync<GanttScheduleDto>(gantt);
        schedule.Groups.SelectMany(g => g.Bars)
            .Where(b => b.ProductionOrderCode == order.Code)
            .Should().ContainSingle().Which.PlannedStart.Should().Be(DueDate.AddMinutes(-60));
        schedule.Groups.Select(g => g.MachineId).Should().NotContain(foreignMachine.Id);
    }

    private static object MoveBody(
        Guid productionOrderId, DateTime plannedStart, DateTime plannedEnd,
        Guid machineId, string? concurrencyToken, bool force, string? notes) => new
        {
            productionOrderId,
            plannedStart,
            plannedEnd,
            machineId,
            concurrencyToken,
            force,
            notes
        };

    private static async Task<IReadOnlyList<Guid>> ConflictingIdsAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var payload = JsonDocument.Parse(body);
        payload.RootElement.TryGetProperty("conflictingSegmentIds", out var ids).Should().BeTrue();
        return ids.EnumerateArray().Select(e => e.GetGuid()).ToList();
    }

    private static string UniqueTag() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

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
        var email = $"gantt305-{suffix}@integration.local";
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

    private static async Task<MachineDto> CreateMachineAsync(HttpClient client, string code)
    {
        var response = await client.PostAsJsonAsync("/api/machines", new
        {
            code,
            name = code,
            description = (string?)null,
            isActive = true
        });
        response.EnsureSuccessStatusCode();
        return await ReadAsync<MachineDto>(response);
    }

    private static async Task<RecipeDto> CreateRecipeAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/recipes", new
        {
            code = $"R-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            name = "Gantt reschedule recipe",
            description = (string?)null,
            isActive = true,
            primaryProductId = (Guid?)null,
            syncId = (string?)null
        });
        response.EnsureSuccessStatusCode();
        return await ReadAsync<RecipeDto>(response);
    }

    private static async Task<OperationDto> CreateOperationAsync(
        HttpClient client, Guid versionId, string suffix, decimal perUnitSeconds)
    {
        var response = await client.PostAsJsonAsync("/api/operations", new
        {
            versionId,
            code = $"OP-{Guid.NewGuid():N}"[..8].ToUpperInvariant() + $"-{suffix}",
            name = $"Operation {suffix}",
            description = (string?)null,
            operationType = (string?)null,
            sortIndex = suffix == "A" ? 0 : 1,
            setupTimeMinutes = (decimal?)null,
            runTimeMode = 1,
            runTimePerUnitSeconds = perUnitSeconds,
            runTimePerBatchMinutes = (decimal?)null,
            teardownTimeMinutes = (decimal?)null,
            queueTimeMinutes = (decimal?)null,
            isOptional = false,
            allowParallelExecution = false,
            expectedQuantity = (decimal?)null
        });
        response.EnsureSuccessStatusCode();
        return await ReadAsync<OperationDto>(response);
    }

    private static async Task AddResourceAsync(HttpClient client, OperationDto operation, Guid machineId)
    {
        var response = await client.PostAsJsonAsync($"/api/operations/{operation.Id}/resources", new
        {
            operationId = operation.Id,
            preferredDepartmentId = (Guid?)null,
            preferredMachineId = machineId,
            requiredCapability = (string?)null,
            requiredOperatorCount = 1,
            requiredRole = (string?)null,
            notes = (string?)null
        });
        response.EnsureSuccessStatusCode();
    }

    private static async Task ReleaseVersionAsync(HttpClient client, Guid versionId)
    {
        var response = await client.PostAsync($"/api/recipe-versions/{versionId}/release", null);
        response.EnsureSuccessStatusCode();
    }

    private async Task<ProductionOrderDto> CreateReleasedOrderAsync(
        HttpClient client, string tag, string suffix, RecipeDto recipe, DateTime? dueDate)
    {
        var orderResponse = await client.PostAsJsonAsync("/api/production-orders", new
        {
            code = $"GNT-{tag}-{suffix}",
            productId = Guid.NewGuid(),
            recipeId = recipe.Id,
            recipeVersionId = recipe.Versions.Single().Id,
            plannedQuantity = 60m,
            measureUnitId = (Guid?)null,
            priority = 0,
            dueDate,
            notes = (string?)null,
            syncId = (string?)null
        });
        orderResponse.EnsureSuccessStatusCode();
        var order = await ReadAsync<ProductionOrderDto>(orderResponse);

        var releaseResponse = await client.PostAsync($"/api/production-orders/{order.Id}/release", null);
        releaseResponse.EnsureSuccessStatusCode();
        return await ReadAsync<ProductionOrderDto>(releaseResponse);
    }

    private sealed record OperationDto(Guid Id, string Code);
}
