using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;
using AsistOff.MES.Multitenancy.Context;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>/api/schedule/operator-queue</c> -
/// the read-only operator shift queue composing the covering roster
/// assignment, Released/InProgress orders overlapping the shift window and the
/// open Andon signals for the queued Work Centers. They exercise the full
/// pipeline (auth, tenant resolution, validation, MediatR, EF Core, exception
/// handler) against a real PostgreSQL database. Ordering assertions only
/// constrain the relative order of the rows seeded here, because the queue
/// shares the database with every other integration test class.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class OperatorShiftQueueEndpointTests(MesApplicationFixture fixture)
    : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/schedule/operator-queue";

    [Fact]
    public async Task GetQueue_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync($"{BaseUrl}?operatorCode=OP-1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetQueue_MissingOperatorCode_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetQueue_UnknownOperatorCode_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{BaseUrl}?operatorCode=OP-NOPE-{UniqueTag()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetQueue_OperatorWithoutAssignment_Returns200EmptyQueue()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var identifier = await CreateOperatorAsync(client);

        var response = await client.GetAsync($"{BaseUrl}?operatorCode={identifier}");

        // An operator with no current shift assignment gets an empty queue
        // plus operator context, not 404.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var queue = await ReadAsync<OperatorShiftQueueDto>(response);
        queue.OperatorCode.Should().Be(identifier);
        queue.OperatorId.Should().NotBeEmpty();
        queue.Shift.Should().BeNull();
        queue.Orders.Should().BeEmpty();
        queue.ActiveSignals.Should().BeEmpty();
    }

    [Fact]
    public async Task GetQueue_HappyPath_ReturnsShiftContext_AndPriorityOrderedQueue()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = UniqueTag();
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var shift = await CreateShiftAsync(client, $"OPQ-{tag}-DAY", "00:00:00", "23:59:59");
        var identifier = await CreateOperatorAsync(client);
        var operatorId = await GetOperatorIdAsync(client, identifier);
        await CreateAssignmentAsync(client, operatorId, shift, today);

        var dueToday = DateTime.UtcNow.Date.AddHours(12);
        var lowPriority = await CreateReleasedOrderAsync(client, tag, "LOW", dueToday, priority: 9);
        var highPriority = await CreateReleasedOrderAsync(client, tag, "HIGH", dueToday, priority: 1);
        var planned = await CreatePlannedOrderAsync(client, tag, "PLN", dueToday);

        var response = await client.GetAsync($"{BaseUrl}?operatorCode={identifier}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var queue = await ReadAsync<OperatorShiftQueueDto>(response);
        queue.OperatorCode.Should().Be(identifier);
        queue.Shift.Should().NotBeNull();
        queue.Shift!.ShiftCode.Should().Be($"OPQ-{tag}-DAY");
        queue.Shift.Date.Should().Be(today);
        queue.Shift.IsOvernight.Should().BeFalse();

        var codes = queue.Orders.Select(o => o.Code).ToList();
        codes.Should().Contain(lowPriority.Code);
        codes.Should().Contain(highPriority.Code);
        codes.Should().NotContain(planned.Code);
        codes.IndexOf(highPriority.Code).Should().BeLessThan(codes.IndexOf(lowPriority.Code));

        var high = queue.Orders.Single(o => o.Code == highPriority.Code);
        high.Priority.Should().Be(1);
        high.PlannedQuantity.Should().Be(100m);
        high.RemainingQuantity.Should().Be(100m);
        high.Status.Should().Be(2); // Released
        queue.Orders.Should().OnlyContain(o => o.Status == 2 || o.Status == 3);

        // No scheduled lanes seed Work Centers here, so no signal scoping
        // applies regardless of other tests' signals.
        queue.ActiveSignals.Should().BeEmpty();
    }

    [Fact]
    public async Task GetQueue_OversizedTake_IsClamped()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = UniqueTag();
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var shift = await CreateShiftAsync(client, $"OPQ-{tag}-DAY", "00:00:00", "23:59:59");
        var identifier = await CreateOperatorAsync(client);
        await CreateAssignmentAsync(client, await GetOperatorIdAsync(client, identifier), shift, today);

        var response = await client.GetAsync($"{BaseUrl}?operatorCode={identifier}&take=5000");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<OperatorShiftQueueDto>(response)).Orders.Should().HaveCountLessThanOrEqualTo(200);
    }

    /// <summary>
    /// Signal scoping through the scheduled lane: the queued order is pinned
    /// to machine A via a <c>ScheduledOperation</c> override, so only the
    /// Active signal on A surfaces (not the one on lane B, nor the Resolved
    /// one on A). Seeded straight through the database in an isolated tenant
    /// so the lane linkage stays exact.
    /// </summary>
    [Fact]
    public async Task GetQueue_WithScheduledLane_ReturnsScopedSignals()
    {
        // Arrange
        var (email, password) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(email);
        var tag = UniqueTag();
        var identifier = $"OP-{tag}";
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (machineA, orderCode, shiftCode) = await SeedLaneAsync(tenantId, tag, identifier, today);
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);

        // Act
        var response = await client.GetAsync($"{BaseUrl}?operatorCode={identifier}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var queue = await ReadAsync<OperatorShiftQueueDto>(response);
        queue.Shift.Should().NotBeNull();
        queue.Shift!.ShiftCode.Should().Be(shiftCode);

        var row = queue.Orders.Should().ContainSingle(o => o.Code == orderCode).Subject;
        row.MachineId.Should().Be(machineA.Id);
        row.MachineCode.Should().Be(machineA.Code);

        var signal = queue.ActiveSignals.Should().ContainSingle().Subject;
        signal.MachineId.Should().Be(machineA.Id);
        signal.MachineCode.Should().Be(machineA.Code);
        signal.Category.Should().Be((short)AndonSignalCategory.Downtime);
        signal.Severity.Should().Be("Downtime");
    }

    private static string UniqueTag() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<(Machine MachineA, string OrderCode, string ShiftCode)> SeedLaneAsync(
        Guid tenantId, string tag, string identifier, DateOnly today)
    {
        var shiftCode = $"OPQ-{tag}-SIG";
        var orderCode = $"OPQ-{tag}-ORD";
        Machine machineA;

        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
            var now = DateTime.UtcNow;

            var shift = new Shift
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = shiftCode,
                Name = shiftCode,
                StartTime = new TimeOnly(0, 0),
                EndTime = new TimeOnly(23, 59, 59),
                IsActive = true
            };
            var @operator = new Operator
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Identifier = identifier,
                FirstName = "Jan",
                LastName = "Kowalski",
                RatePerHour = 10m,
                UserId = Guid.Empty
            };
            machineA = new Machine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = $"OPQ-{tag}-A",
                Name = $"OPQ-{tag}-A",
                CreatedAt = now
            };
            var machineB = new Machine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = $"OPQ-{tag}-B",
                Name = $"OPQ-{tag}-B",
                CreatedAt = now
            };
            var order = new ProductionOrder
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = orderCode,
                ProductId = Guid.NewGuid(),
                RecipeId = Guid.NewGuid(),
                RecipeVersionId = Guid.NewGuid(),
                PlannedQuantity = 100m,
                Status = ProductionOrderStatus.Released,
                DueDate = now.Date.AddHours(12),
                Priority = 1,
                CreatedAt = now
            };
            context.Set<Shift>().Add(shift);
            context.Set<Operator>().Add(@operator);
            context.Set<Machine>().Add(machineA);
            context.Set<Machine>().Add(machineB);
            context.Set<ProductionOrder>().Add(order);
            context.Set<OperatorShiftAssignment>().Add(new OperatorShiftAssignment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OperatorId = @operator.Id,
                ShiftId = shift.Id,
                Date = today
            });
            context.Set<ScheduledOperation>().Add(new ScheduledOperation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductionOrderId = order.Id,
                OperationNodeId = Guid.NewGuid(),
                MachineId = machineA.Id,
                PlannedStart = now.Date.AddHours(7),
                PlannedEnd = now.Date.AddHours(9),
                CreatedAt = now
            });
            context.Set<AndonSignal>().Add(new AndonSignal
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MachineId = machineA.Id,
                Category = AndonSignalCategory.Downtime,
                Status = AndonSignalStatus.Active,
                RaisedAt = now.AddMinutes(-30),
                CreatedAt = now
            });
            context.Set<AndonSignal>().Add(new AndonSignal
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MachineId = machineB.Id,
                Category = AndonSignalCategory.Quality,
                Status = AndonSignalStatus.Active,
                RaisedAt = now.AddMinutes(-15),
                CreatedAt = now
            });
            context.Set<AndonSignal>().Add(new AndonSignal
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MachineId = machineA.Id,
                Category = AndonSignalCategory.Material,
                Status = AndonSignalStatus.Resolved,
                RaisedAt = now.AddMinutes(-45),
                ResolvedAt = now.AddMinutes(-5),
                CreatedAt = now
            });

            await context.SaveChangesAsync();
        }

        return (machineA, orderCode, shiftCode);
    }

    private async Task<Guid> GetTenantIdByEmailAsync(string email)
    {
        using var scope = Fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MultitenancyDbContext>();
        var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.ContactEmail == email);
        return tenant.Id;
    }

    private static async Task<ShiftDto> CreateShiftAsync(
        HttpClient client, string code, string startTime, string endTime)
    {
        var response = await client.PostAsJsonAsync("/api/shifts", new
        {
            code,
            name = code,
            description = (string?)null,
            startTime,
            endTime,
            isActive = true
        });
        response.EnsureSuccessStatusCode();
        return await ReadAsync<ShiftDto>(response);
    }

    private static async Task<string> CreateOperatorAsync(HttpClient client)
    {
        var identifier = $"OP-{Guid.NewGuid():N}"[..12];
        var response = await client.PostAsJsonAsync("/api/operators", new
        {
            identifier,
            firstName = "Jan",
            lastName = "Kowalski",
            ratePerHour = 10m,
            departmentId = (Guid?)null,
            userId = Guid.Empty
        });
        response.EnsureSuccessStatusCode();
        return (await ReadAsync<OperatorDto>(response)).Identifier;
    }

    private static async Task<Guid> GetOperatorIdAsync(HttpClient client, string identifier)
    {
        var response = await client.GetAsync($"/api/operators?identifier={identifier}");
        response.EnsureSuccessStatusCode();
        var page = await ReadAsync<PagedResponseDto<OperatorDto>>(response);
        return page.Items.Single(o => o.Identifier == identifier).Id;
    }

    private static async Task CreateAssignmentAsync(HttpClient client, Guid operatorId, ShiftDto shift, string date)
    {
        var response = await client.PostAsJsonAsync("/api/operator-shift-assignments", new
        {
            operatorId,
            shiftId = shift.Id,
            date,
            notes = (string?)null
        });
        response.EnsureSuccessStatusCode();
    }

    private async Task<ProductionOrderDto> CreateReleasedOrderAsync(
        HttpClient client, string tag, string suffix, DateTime dueDate, int priority = 0)
    {
        var recipe = await CreateRecipeAsync(client);
        var versionId = recipe.Versions.Single().Id;

        var operationResponse = await client.PostAsJsonAsync("/api/operations", new
        {
            versionId,
            code = $"OP-{Guid.NewGuid():N}"[..8],
            name = "Cutting",
            description = (string?)null,
            operationType = (string?)null,
            sortIndex = 0,
            setupTimeMinutes = (decimal?)null,
            runTimeMode = 1,
            runTimePerUnitSeconds = 10m,
            runTimePerBatchMinutes = (decimal?)null,
            teardownTimeMinutes = (decimal?)null,
            queueTimeMinutes = (decimal?)null,
            isOptional = false,
            allowParallelExecution = false,
            expectedQuantity = (decimal?)null
        });
        operationResponse.EnsureSuccessStatusCode();

        var releaseVersionResponse = await client.PostAsync($"/api/recipe-versions/{versionId}/release", null);
        releaseVersionResponse.EnsureSuccessStatusCode();

        var orderResponse = await client.PostAsJsonAsync("/api/production-orders", new
        {
            code = $"OPQ-{tag}-{suffix}",
            productId = Guid.NewGuid(),
            recipeId = recipe.Id,
            recipeVersionId = versionId,
            plannedQuantity = 100m,
            measureUnitId = (Guid?)null,
            priority,
            dueDate,
            notes = (string?)null,
            syncId = (string?)null
        });
        orderResponse.EnsureSuccessStatusCode();
        var order = await ReadAsync<ProductionOrderDto>(orderResponse);

        var releaseOrderResponse = await client.PostAsync($"/api/production-orders/{order.Id}/release", null);
        releaseOrderResponse.EnsureSuccessStatusCode();
        return await ReadAsync<ProductionOrderDto>(releaseOrderResponse);
    }

    private async Task<ProductionOrderDto> CreatePlannedOrderAsync(
        HttpClient client, string tag, string suffix, DateTime dueDate)
    {
        var response = await client.PostAsJsonAsync("/api/production-orders", new
        {
            code = $"OPQ-{tag}-{suffix}",
            productId = Guid.NewGuid(),
            recipeId = Guid.NewGuid(),
            recipeVersionId = Guid.NewGuid(),
            plannedQuantity = 100m,
            measureUnitId = (Guid?)null,
            priority = 0,
            dueDate,
            notes = (string?)null,
            syncId = (string?)null
        });
        response.EnsureSuccessStatusCode();
        return await ReadAsync<ProductionOrderDto>(response);
    }

    private static async Task<RecipeDto> CreateRecipeAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/recipes", new
        {
            code = $"R-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            name = "Queue recipe",
            description = (string?)null,
            isActive = true,
            primaryProductId = (Guid?)null,
            syncId = (string?)null
        });
        response.EnsureSuccessStatusCode();
        return await ReadAsync<RecipeDto>(response);
    }

    private sealed record OperatorDto(Guid Id, string Identifier);
}
