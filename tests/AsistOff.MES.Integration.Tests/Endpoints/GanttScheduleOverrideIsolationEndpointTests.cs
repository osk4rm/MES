using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;
using AsistOff.MES.Multitenancy.Context;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Persistence-level integration tests for the <c>ScheduledOperation</c>
/// override table (issue #304, Gantt slice 1/3). Slice 1 ships no write API,
/// so these tests drive <see cref="IScheduledOperationsRepository"/> and
/// <see cref="DefaultContext"/> directly against Testcontainers PostgreSQL,
/// proving the migration, the tenant-scoped unique constraint and the global
/// query filter isolation — including that a foreign tenant's override rows
/// never overlay the Gantt read-model served over HTTP.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class GanttScheduleOverrideIsolationEndpointTests(MesApplicationFixture fixture)
    : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/schedule/gantt";

    // A window no other test class seeds into, so bars stay ours.
    private const string From = "2027-05-10";
    private const string To = "2027-05-12";

    [Fact]
    public async Task Migration_Applies_ScheduledOperationsTableExists()
    {
        using var scope = Fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();

        var applied = await context.Database.GetAppliedMigrationsAsync();
        var canQuery = await context.Set<ScheduledOperation>().IgnoreQueryFilters().CountAsync() >= 0;

        applied.Should().Contain(m => m.Contains("AddScheduledOperations"));
        canQuery.Should().BeTrue();
    }

    [Fact]
    public async Task DuplicateOverride_SameTenant_Fails()
    {
        var tenantId = await GetTenantIdByNameAsync("dev");
        var orderId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var machineId = Guid.NewGuid();
        var window = new DateTime(2027, 5, 11, 8, 0, 0, DateTimeKind.Utc);

        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var overrides = scope.ServiceProvider.GetRequiredService<IScheduledOperationsRepository>();

            await overrides.AddAsync(new ScheduledOperation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductionOrderId = orderId,
                OperationNodeId = operationId,
                MachineId = machineId,
                PlannedStart = window,
                PlannedEnd = window.AddHours(1),
                CreatedAt = DateTime.UtcNow,
            });

            var duplicate = () => overrides.AddAsync(new ScheduledOperation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductionOrderId = orderId,
                OperationNodeId = operationId,
                MachineId = machineId,
                PlannedStart = window,
                PlannedEnd = window.AddHours(1),
                CreatedAt = DateTime.UtcNow,
            });

            // Tenant-scoped unique index rejects the second row for the same
            // (order, operation) within one tenant.
            await duplicate.Should().ThrowAsync<DbUpdateException>();
        }
    }

    [Fact]
    public async Task SameOverrideKeys_DifferentTenants_Succeeds()
    {
        var devTenantId = await GetTenantIdByNameAsync("dev");
        var (otherEmail, _) = await Fixture.CreateTenantAsync();
        var otherTenantId = await GetTenantIdByEmailAsync(otherEmail);
        var orderId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var window = new DateTime(2027, 5, 11, 8, 0, 0, DateTimeKind.Utc);

        using (BackgroundTenantContext.BeginScope(devTenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var overrides = scope.ServiceProvider.GetRequiredService<IScheduledOperationsRepository>();
            var created = await overrides.AddAsync(new ScheduledOperation
            {
                Id = Guid.NewGuid(),
                TenantId = devTenantId,
                ProductionOrderId = orderId,
                OperationNodeId = operationId,
                MachineId = Guid.NewGuid(),
                PlannedStart = window,
                PlannedEnd = window.AddHours(1),
                CreatedAt = DateTime.UtcNow,
            });
            created.ProductionOrderId.Should().Be(orderId);
        }

        using (BackgroundTenantContext.BeginScope(otherTenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var overrides = scope.ServiceProvider.GetRequiredService<IScheduledOperationsRepository>();
            var created = await overrides.AddAsync(new ScheduledOperation
            {
                Id = Guid.NewGuid(),
                TenantId = otherTenantId,
                ProductionOrderId = orderId,
                OperationNodeId = operationId,
                MachineId = Guid.NewGuid(),
                PlannedStart = window,
                PlannedEnd = window.AddHours(1),
                CreatedAt = DateTime.UtcNow,
            });

            created.ProductionOrderId.Should().Be(orderId);
        }
    }

    [Fact]
    public async Task CrossTenantOverrides_NeverOverlayInGantt()
    {
        // Arrange - a real released order in the dev tenant with computed
        // timing: 60s/unit x 60 units = 60min, backward-anchored at dueDate.
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var machine = await CreateMachineAsync(client, $"GNT-{tag}-WC");
        var recipe = await CreateRecipeAsync(client);
        var versionId = recipe.Versions.Single().Id;
        var op = await CreateOperationAsync(client, versionId, "A", perUnitSeconds: 60m);
        await AddResourceAsync(client, op, machine.Id);
        await ReleaseVersionAsync(client, versionId);
        var dueDate = new DateTime(2027, 5, 11, 12, 0, 0, DateTimeKind.Utc);
        var order = await CreateReleasedOrderAsync(client, tag, "OVR", recipe, dueDate);

        // A second tenant plants an override row carrying the same
        // (ProductionOrderId, OperationNodeId) but pinned to a foreign lane
        // and a window outside the computed bars.
        var (otherEmail, _) = await Fixture.CreateTenantAsync();
        var otherTenantId = await GetTenantIdByEmailAsync(otherEmail);
        var foreignMachineId = Guid.NewGuid();
        var foreignStart = new DateTime(2027, 5, 11, 2, 0, 0, DateTimeKind.Utc);
        using (BackgroundTenantContext.BeginScope(otherTenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var overrides = scope.ServiceProvider.GetRequiredService<IScheduledOperationsRepository>();
            await overrides.AddAsync(new ScheduledOperation
            {
                Id = Guid.NewGuid(),
                TenantId = otherTenantId,
                ProductionOrderId = order.Id,
                OperationNodeId = op.Id,
                MachineId = foreignMachineId,
                PlannedStart = foreignStart,
                PlannedEnd = foreignStart.AddHours(3),
                Notes = "foreign override",
                CreatedAt = DateTime.UtcNow,
            });
        }

        // The dev tenant's repository view must not contain the foreign row.
        var devTenantId = await GetTenantIdByNameAsync("dev");
        using (BackgroundTenantContext.BeginScope(devTenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var overrides = scope.ServiceProvider.GetRequiredService<IScheduledOperationsRepository>();
            var visible = await overrides.ListForOrdersAsync([order.Id]);
            visible.Should().NotContain(o => o.Notes == "foreign override");
        }

        // Act - read the Gantt schedule as the dev tenant.
        var response = await client.GetAsync($"{BaseUrl}?from={From}&to={To}");

        // Assert - the computed bar survives; the foreign pinned window and
        // lane never overlay it.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var schedule = await ReadAsync<GanttScheduleDto>(response);
        var bars = schedule.Groups.SelectMany(g => g.Bars)
            .Where(b => b.ProductionOrderCode == order.Code).ToList();
        bars.Should().ContainSingle();
        var bar = bars.Single();
        bar.OperationNodeId.Should().Be(op.Id);
        bar.PlannedStart.Should().Be(dueDate.AddMinutes(-60));
        bar.PlannedEnd.Should().Be(dueDate);
        bar.MachineId.Should().Be(machine.Id);
        schedule.Groups.SelectMany(g => g.Bars).Select(b => b.MachineId)
            .Should().NotContain(foreignMachineId);
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

    private static string UniqueTag() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

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
            name = "Gantt override recipe",
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
            sortIndex = 0,
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
