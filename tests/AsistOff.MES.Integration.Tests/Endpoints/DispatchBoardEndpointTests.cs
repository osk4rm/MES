using System.Net;
using System.Net.Http.Json;
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
/// Endpoint-scoped integration tests for <c>/api/schedule/dispatch</c> - the
/// read-only shift-aware dispatch board over Released Production Orders. They
/// exercise the full request pipeline (auth, tenant resolution, the read-time
/// aggregation and the global exception handler) against a real PostgreSQL
/// database. The board shares the database with every other integration test
/// class, so ordering assertions only constrain the relative order of the
/// rows seeded here.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class DispatchBoardEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/schedule/dispatch";

    // A window no other test class seeds into, so day buckets stay ours.
    private const string From = "2027-03-10";
    private const string To = "2027-03-12";

    [Fact]
    public async Task GetDispatch_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync($"{BaseUrl}?from={From}&to={To}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDispatch_MissingDates_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetDispatch_HappyPath_ReturnsBucketsWithShiftsHeadcountsAndOrderedRows()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = UniqueTag();
        var morning = await CreateShiftAsync(client, $"DSP-{tag}-AM");
        var night = await CreateShiftAsync(client, $"DSP-{tag}-NI", "22:00:00", "06:00:00");
        var operatorA = await CreateOperatorAsync(client);
        var operatorB = await CreateOperatorAsync(client);
        await CreateAssignmentAsync(client, operatorA, morning, From);
        await CreateAssignmentAsync(client, operatorB, morning, From);
        await CreateAssignmentAsync(client, operatorA, night, "2027-03-11");

        var overdue = await CreateReleasedOrderAsync(client, tag, "OVD", dueDate: new DateTime(2027, 3, 8, 0, 0, 0, DateTimeKind.Utc), priority: 5);
        var dueSoon = await CreateReleasedOrderAsync(client, tag, "SOON", dueDate: new DateTime(2027, 3, 11, 0, 0, 0, DateTimeKind.Utc), priority: 9);
        var noDue = await CreateReleasedOrderAsync(client, tag, "NODUE", dueDate: null, priority: 0);

        // One confirmation so the row carries read-time totals.
        var confirm = await client.PostAsJsonAsync("/api/production-confirmations", new
        {
            productionOrderId = dueSoon.Id,
            machineId = Guid.NewGuid(),
            reportedByOperatorId = (Guid?)null,
            reportedAt = DateTime.UtcNow,
            goodQuantity = 20m,
            scrapQuantity = 2m,
            notes = (string?)null
        });
        confirm.EnsureSuccessStatusCode();

        var response = await client.GetAsync($"{BaseUrl}?from={From}&to={To}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var board = await ReadAsync<DispatchBoardDto>(response);

        board.From.Should().Be(From);
        board.To.Should().Be(To);
        board.Days.Should().HaveCount(3);
        board.Days.Select(d => d.Date).Should().ContainInOrder(From, "2027-03-11", To);

        var firstDay = board.Days.Single(d => d.Date == From);
        firstDay.Shifts.Should().ContainSingle(s => s.Code == $"DSP-{tag}-AM").Which.Headcount.Should().Be(2);
        firstDay.Shifts.Single(s => s.Code == $"DSP-{tag}-AM").IsOvernight.Should().BeFalse();
        firstDay.Shifts.Single(s => s.Code == $"DSP-{tag}-AM").IsUncovered.Should().BeFalse();
        var nightShift = firstDay.Shifts.Single(s => s.Code == $"DSP-{tag}-NI");
        nightShift.Headcount.Should().Be(0);
        nightShift.IsOvernight.Should().BeTrue();
        nightShift.IsUncovered.Should().BeTrue();

        var secondDay = board.Days.Single(d => d.Date == "2027-03-11");
        secondDay.Shifts.Single(s => s.Code == $"DSP-{tag}-NI").Headcount.Should().Be(1);
        secondDay.Shifts.Single(s => s.Code == $"DSP-{tag}-NI").IsUncovered.Should().BeFalse();
        secondDay.Shifts.Single(s => s.Code == $"DSP-{tag}-AM").Headcount.Should().Be(0);
        secondDay.Shifts.Single(s => s.Code == $"DSP-{tag}-AM").IsUncovered.Should().BeTrue();

        // Overdue first, then due-date ascending, nulls last.
        var codes = board.Orders.Select(o => o.Code).ToList();
        codes.Should().Contain(overdue.Code);
        codes.Should().Contain(dueSoon.Code);
        codes.Should().Contain(noDue.Code);
        codes.IndexOf(overdue.Code).Should().BeLessThan(codes.IndexOf(dueSoon.Code));
        codes.IndexOf(dueSoon.Code).Should().BeLessThan(codes.IndexOf(noDue.Code));

        board.Orders.Single(o => o.Code == overdue.Code).IsOverdue.Should().BeTrue();
        board.Orders.Single(o => o.Code == dueSoon.Code).IsOverdue.Should().BeFalse();
        board.Orders.Single(o => o.Code == noDue.Code).IsOverdue.Should().BeFalse();

        var confirmed = board.Orders.Single(o => o.Code == dueSoon.Code);
        confirmed.ProducedQuantity.Should().Be(20m);
        confirmed.ScrappedQuantity.Should().Be(2m);
        confirmed.RemainingQuantity.Should().Be(80m);
        confirmed.Status.Should().Be(3); // InProgress after the confirmation
    }

    [Fact]
    public async Task GetDispatch_FlagsUncoveredShift_AndClearsAfterAssignment()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = UniqueTag();
        var shift = await CreateShiftAsync(client, $"DSP-{tag}-EMPTY");

        // Empty shift is flagged uncovered on every day of the window.
        var emptyResponse = await client.GetAsync($"{BaseUrl}?from={From}&to={To}");

        emptyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var emptyBoard = await ReadAsync<DispatchBoardDto>(emptyResponse);
        var emptyEntries = emptyBoard.Days.Select(d => d.Shifts.Single(s => s.Code == $"DSP-{tag}-EMPTY")).ToList();
        emptyEntries.Should().OnlyContain(s => s.Headcount == 0);
        emptyEntries.Should().OnlyContain(s => s.IsUncovered);

        // Assigning an operator for the first day clears the flag only there.
        var operatorId = await CreateOperatorAsync(client);
        await CreateAssignmentAsync(client, operatorId, shift, From);

        var coveredResponse = await client.GetAsync($"{BaseUrl}?from={From}&to={To}");

        coveredResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var coveredBoard = await ReadAsync<DispatchBoardDto>(coveredResponse);

        var coveredDay = coveredBoard.Days.Single(d => d.Date == From);
        var coveredShift = coveredDay.Shifts.Single(s => s.Code == $"DSP-{tag}-EMPTY");
        coveredShift.Headcount.Should().Be(1);
        coveredShift.IsUncovered.Should().BeFalse();

        coveredBoard.Days.Where(d => d.Date != From)
            .Select(d => d.Shifts.Single(s => s.Code == $"DSP-{tag}-EMPTY"))
            .Should().OnlyContain(s => s.IsUncovered);
    }

    [Fact]
    public async Task GetDispatch_ExcludesCompletedClosedPlannedAndAfterWindowOrders()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = UniqueTag();
        var inWindow = new DateTime(2027, 3, 11, 0, 0, 0, DateTimeKind.Utc);

        var planned = await CreateOrderAsync(client, tag, "PLN", inWindow);
        var completed = await CreateCompletedOrderAsync(client, tag, "CMP", inWindow);
        var afterWindow = await CreateReleasedOrderAsync(
            client, tag, "AFT", dueDate: new DateTime(2027, 4, 5, 0, 0, 0, DateTimeKind.Utc));
        var visible = await CreateReleasedOrderAsync(client, tag, "VIS", dueDate: inWindow);

        var close = await client.PostAsync($"/api/production-orders/{completed.Id}/close", null);
        close.EnsureSuccessStatusCode();
        var closedCode = (await ReadAsync<ProductionOrderDto>(close)).Code;

        var response = await client.GetAsync($"{BaseUrl}?from={From}&to={To}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var board = await ReadAsync<DispatchBoardDto>(response);
        var codes = board.Orders.Select(o => o.Code).ToList();

        codes.Should().Contain(visible.Code);
        codes.Should().NotContain(planned.Code);
        codes.Should().NotContain(completed.Code);
        codes.Should().NotContain(closedCode);
        codes.Should().NotContain(afterWindow.Code);
        board.Orders.Should().OnlyContain(o => o.Status == 2 || o.Status == 3);
    }

    [Fact]
    public async Task GetDispatch_ReversedWindow_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{BaseUrl}?from={To}&to={From}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetDispatch_WindowBoundaries_32Days400_31Days200()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var tooWide = await client.GetAsync($"{BaseUrl}?from=2027-03-01&to=2027-04-01");

        tooWide.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var maxWide = await client.GetAsync($"{BaseUrl}?from=2027-03-01&to=2027-03-31");

        maxWide.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<DispatchBoardDto>(maxWide)).Days.Should().HaveCount(31);
    }

    [Fact]
    public async Task GetDispatch_OrdersAndRosterOfAnotherTenant_AreNotVisible()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var tag = UniqueTag();
        var foreignShift = await CreateShiftAsync(otherTenantClient, $"DSP-{tag}-AM");
        var foreignOperator = await CreateOperatorAsync(otherTenantClient);
        await CreateAssignmentAsync(otherTenantClient, foreignOperator, foreignShift, From);
        var foreignOrder = await CreateReleasedOrderAsync(
            otherTenantClient, tag, "FRN", dueDate: new DateTime(2027, 3, 11, 0, 0, 0, DateTimeKind.Utc));

        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync($"{BaseUrl}?from={From}&to={To}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var board = await ReadAsync<DispatchBoardDto>(response);

        board.Orders.Select(o => o.Code).Should().NotContain(foreignOrder.Code);
        board.Days.SelectMany(d => d.Shifts).Select(s => s.Code).Should().NotContain($"DSP-{tag}-AM");
    }

    private static string UniqueTag() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    /// <summary>
    /// Read-path hardening (issue #274): with 250 Released orders the board
    /// issues a bounded query returning at most 200 rows, overdue first.
    /// Seeded straight through the database in an isolated tenant - 250 HTTP
    /// creates would be slow and flaky, and isolation keeps the bound exact.
    /// </summary>
    [Fact]
    public async Task GetDispatch_With250ReleasedOrders_Returns200WithOverdueFirst()
    {
        // Arrange
        var (email, password) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(email);
        var tag = UniqueTag();
        var overdueCodes = Enumerable.Range(0, 10).Select(i => $"DSP-{tag}-OVD-{i:000}").OrderBy(c => c).ToList();
        var inWindowCodes = Enumerable.Range(0, 230).Select(i => $"DSP-{tag}-INW-{i:000}").ToList();
        var noDueCodes = Enumerable.Range(0, 10).Select(i => $"DSP-{tag}-NOD-{i:000}").ToList();
        await SeedReleasedOrdersAsync(tenantId, overdueCodes, new DateTime(2027, 3, 5, 0, 0, 0, DateTimeKind.Utc));
        await SeedReleasedOrdersAsync(tenantId, inWindowCodes, new DateTime(2027, 3, 11, 0, 0, 0, DateTimeKind.Utc));
        await SeedReleasedOrdersAsync(tenantId, noDueCodes, null);
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);

        // Act
        var response = await client.GetAsync($"{BaseUrl}?from={From}&to={To}");

        // Assert - bounded to 200 rows with overdue first and nulls last.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var board = await ReadAsync<DispatchBoardDto>(response);
        board.Orders.Should().HaveCount(200);
        board.Orders.Should().OnlyContain(o => o.Code.StartsWith($"DSP-{tag}-"));
        board.Orders.Take(10).Select(o => o.Code).Should().Equal(overdueCodes);
        board.Orders.Take(10).Should().OnlyContain(o => o.IsOverdue);
        board.Orders.Skip(10).Should().OnlyContain(o => !o.IsOverdue);
    }

    private async Task SeedReleasedOrdersAsync(Guid tenantId, IReadOnlyCollection<string> codes, DateTime? dueDate)
    {
        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
            foreach (var code in codes)
            {
                context.Set<ProductionOrder>().Add(new ProductionOrder
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Code = code,
                    ProductId = Guid.NewGuid(),
                    RecipeId = Guid.NewGuid(),
                    RecipeVersionId = Guid.NewGuid(),
                    PlannedQuantity = 100m,
                    Status = ProductionOrderStatus.Released,
                    DueDate = dueDate,
                    Priority = 0,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await context.SaveChangesAsync();
        }
    }

    private async Task<Guid> GetTenantIdByEmailAsync(string email)
    {
        using var scope = Fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MultitenancyDbContext>();
        var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.ContactEmail == email);
        return tenant.Id;
    }

    private static async Task<ShiftDto> CreateShiftAsync(
        HttpClient client, string code, string startTime = "06:00:00", string endTime = "14:00:00")
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

    private static async Task<Guid> CreateOperatorAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/operators", new
        {
            identifier = $"OP-{Guid.NewGuid():N}"[..12],
            firstName = "Jan",
            lastName = "Kowalski",
            ratePerHour = 10m,
            departmentId = (Guid?)null,
            userId = Guid.Empty
        });
        response.EnsureSuccessStatusCode();
        return (await ReadAsync<OperatorDto>(response)).Id;
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

    private async Task<ProductionOrderDto> CreateOrderAsync(HttpClient client, string tag, string suffix, DateTime? dueDate)
    {
        var response = await client.PostAsJsonAsync("/api/production-orders", new
        {
            code = $"DSP-{tag}-{suffix}",
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

    private async Task<ProductionOrderDto> CreateReleasedOrderAsync(
        HttpClient client, string tag, string suffix, DateTime? dueDate, int priority = 0)
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
            code = $"DSP-{tag}-{suffix}",
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

    private async Task<ProductionOrderDto> CreateCompletedOrderAsync(
        HttpClient client, string tag, string suffix, DateTime? dueDate)
    {
        var order = await CreateReleasedOrderAsync(client, tag, suffix, dueDate);
        var confirm = await client.PostAsJsonAsync("/api/production-confirmations", new
        {
            productionOrderId = order.Id,
            machineId = Guid.NewGuid(),
            reportedByOperatorId = (Guid?)null,
            reportedAt = DateTime.UtcNow,
            goodQuantity = 10m,
            scrapQuantity = 0m,
            notes = (string?)null
        });
        confirm.EnsureSuccessStatusCode();

        var complete = await client.PostAsync($"/api/production-orders/{order.Id}/complete", null);
        complete.EnsureSuccessStatusCode();
        return await ReadAsync<ProductionOrderDto>(complete);
    }

    private static async Task<RecipeDto> CreateRecipeAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/recipes", new
        {
            code = $"R-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            name = "Dispatch recipe",
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
