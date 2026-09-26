using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>/api/shift-handovers/context</c> -
/// the read-only shift handover context (slice 1/2, issue #292). They exercise
/// the full request pipeline (auth, tenant resolution, shift-window resolution,
/// the read-time composition and the global exception handler) against a real
/// PostgreSQL database. The context shares the database with every other
/// integration test class, so assertions only constrain the rows seeded here.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ShiftHandoverContextEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/shift-handovers/context";

    [Fact]
    public async Task GetContext_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();
        var to = DateTime.UtcNow;
        var from = to.AddHours(-2);

        var response = await client.GetAsync($"{BaseUrl}?from={Q(from)}&to={Q(to)}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetContext_MissingDates_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetContext_ReversedWindow_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var to = DateTime.UtcNow;
        var from = to.AddHours(-2);

        var response = await client.GetAsync($"{BaseUrl}?from={Q(to)}&to={Q(from)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetContext_WindowOver24Hours_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var to = DateTime.UtcNow;
        var from = to.AddHours(-25);

        var response = await client.GetAsync($"{BaseUrl}?from={Q(from)}&to={Q(to)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetContext_UnknownMachine_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var to = DateTime.UtcNow;
        var from = to.AddHours(-2);

        var response = await client.GetAsync($"{BaseUrl}?machineId={Guid.NewGuid()}&from={Q(from)}&to={Q(to)}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetContext_HappyPath_ReturnsOrdersSignalsConfirmationsAndShift()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = UniqueTag();
        var machine = await CreateMachineAsync(client, tag);
        var shift = await CreateShiftAsync(client, tag);
        var reason = await CreateReasonCodeAsync(client, tag);
        var operatorId = await CreateOperatorAsync(client);

        var to = DateTime.UtcNow;
        var from = to.AddHours(-2);
        await PutCalendarCoveringAsync(client, machine.Id, from, shift.Id);

        // One open order; the confirmation below flips it Released -> InProgress
        // and contributes the read-time totals.
        var order = await CreateReleasedOrderAsync(client, tag, "ORD");
        var firstResponse = await ConfirmAsync(client, order.Id, machine.Id, to.AddMinutes(-40), 20m, 2m, operatorId);
        firstResponse.EnsureSuccessStatusCode();
        var first = await ReadAsync<ProductionConfirmationDto>(firstResponse);
        var secondResponse = await ConfirmAsync(client, order.Id, machine.Id, to.AddMinutes(-10), 5m, 0m, null);
        secondResponse.EnsureSuccessStatusCode();
        var second = await ReadAsync<ProductionConfirmationDto>(secondResponse);

        // Acknowledge + resolve round-trips free the machine so only the last
        // raised signal stays Active; earlier ones must be excluded.
        var acked = await RaiseAndonAsync(client, machine.Id, raisedAt: to.AddMinutes(-50));
        var ack = await client.PostAsJsonAsync($"/api/andon-signals/{acked.Id}/acknowledge", new { });
        ack.EnsureSuccessStatusCode();
        var resolved = await RaiseAndonAsync(client, machine.Id, raisedAt: to.AddMinutes(-45));
        var res = await client.PostAsJsonAsync($"/api/andon-signals/{resolved.Id}/resolve", new { });
        res.EnsureSuccessStatusCode();
        var active = await RaiseAndonAsync(client, machine.Id, reason.Id, to.AddMinutes(-5));

        var response = await client.GetAsync($"{BaseUrl}?machineId={machine.Id}&from={Q(from)}&to={Q(to)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var context = await ReadAsync<ShiftHandoverContextDto>(response);

        context.MachineId.Should().Be(machine.Id);
        context.ShiftId.Should().Be(shift.Id);
        context.UncoveredShift.Should().BeFalse();

        // Open orders carry code, product, planned/produced, priority, due date.
        var row = context.OpenOrders.Should().ContainSingle(o => o.Code == order.Code).Subject;
        row.ProductId.Should().Be(order.ProductId);
        row.PlannedQuantity.Should().Be(100m);
        row.ProducedQuantity.Should().Be(25m);
        row.ScrappedQuantity.Should().Be(2m);
        row.Status.Should().Be((short)3); // InProgress after the confirmations

        // Only the Active signal, with reason code and severity.
        var signal = context.ActiveSignals.Should().ContainSingle(s => s.Id == active.Id).Subject;
        signal.Id.Should().Be(active.Id);
        signal.Code.Should().Be(reason.Code);
        signal.Severity.Should().Be("Downtime");
        signal.RaisedAt.Should().BeCloseTo(active.RaisedAt, TimeSpan.FromSeconds(1));

        // Recent confirmations newest-first with quantities, Work Center, operator code.
        context.Confirmations.Should().HaveCount(2);
        context.Confirmations.Select(c => c.Id).Should().ContainInOrder(second.Id, first.Id);
        context.Confirmations.Should().OnlyContain(c => c.MachineId == machine.Id);
        context.Confirmations.Single(c => c.Id == first.Id).OperatorCode.Should().Be(operatorId.Identifier);
        context.Confirmations.Single(c => c.Id == second.Id).OperatorCode.Should().BeNull();
        context.ConfirmationsTotalCount.Should().Be(2);
        context.ConfirmationPage.Should().Be(1);
        context.ConfirmationPageSize.Should().Be(20);
    }

    [Fact]
    public async Task GetContext_MachineWithoutCalendar_ReturnsUncoveredShift()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = UniqueTag();
        var machine = await CreateMachineAsync(client, tag);
        var to = DateTime.UtcNow;
        var from = to.AddHours(-2);

        var response = await client.GetAsync($"{BaseUrl}?machineId={machine.Id}&from={Q(from)}&to={Q(to)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var context = await ReadAsync<ShiftHandoverContextDto>(response);

        context.MachineId.Should().Be(machine.Id);
        context.ShiftId.Should().BeNull();
        context.UncoveredShift.Should().BeTrue();
        context.ActiveSignals.Should().NotContain(s => s.MachineId == machine.Id);
    }

    [Fact]
    public async Task GetContext_NoMachineScope_IsTenantWideWithoutShift()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = UniqueTag();
        var machine = await CreateMachineAsync(client, tag);
        var to = DateTime.UtcNow;
        var from = to.AddHours(-2);
        var signal = await RaiseAndonAsync(client, machine.Id, raisedAt: to.AddMinutes(-5));

        var response = await client.GetAsync($"{BaseUrl}?from={Q(from)}&to={Q(to)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var context = await ReadAsync<ShiftHandoverContextDto>(response);

        context.MachineId.Should().BeNull();
        context.ShiftId.Should().BeNull();
        context.UncoveredShift.Should().BeFalse();
        context.ActiveSignals.Should().ContainSingle(s => s.Id == signal.Id);
    }

    [Fact]
    public async Task GetContext_ConfirmationsPaging_Default20_Max100()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = UniqueTag();
        var machine = await CreateMachineAsync(client, tag);
        var to = DateTime.UtcNow;
        var from = to.AddHours(-2);
        var order = await CreateReleasedOrderAsync(client, tag, "PAG");

        for (var i = 0; i < 3; i++)
        {
            var confirm = await ConfirmAsync(
                client, order.Id, machine.Id, to.AddMinutes(-30 + i), 1m, 0m, null);
            confirm.EnsureSuccessStatusCode();
        }

        var defaultPage = await ReadAsync<ShiftHandoverContextDto>(
            await client.GetAsync($"{BaseUrl}?machineId={machine.Id}&from={Q(from)}&to={Q(to)}"));

        defaultPage.Confirmations.Should().HaveCount(3);
        defaultPage.Confirmations.Should().BeInDescendingOrder(c => c.ReportedAt);
        defaultPage.ConfirmationPageSize.Should().Be(20);

        var firstPage = await ReadAsync<ShiftHandoverContextDto>(
            await client.GetAsync($"{BaseUrl}?machineId={machine.Id}&from={Q(from)}&to={Q(to)}&confirmationPage=1&confirmationPageSize=2"));

        firstPage.Confirmations.Should().HaveCount(2);
        firstPage.ConfirmationsTotalCount.Should().Be(3);
        firstPage.ConfirmationPage.Should().Be(1);

        var secondPage = await ReadAsync<ShiftHandoverContextDto>(
            await client.GetAsync($"{BaseUrl}?machineId={machine.Id}&from={Q(from)}&to={Q(to)}&confirmationPage=2&confirmationPageSize=2"));

        var oldest = secondPage.Confirmations.Should().ContainSingle().Subject;
        firstPage.Confirmations.Select(c => c.Id).Should().NotContain(oldest.Id);
        oldest.ReportedAt.Should().Be(
            firstPage.Confirmations.Concat(secondPage.Confirmations).Min(c => c.ReportedAt));

        var tooBig = await client.GetAsync(
            $"{BaseUrl}?machineId={machine.Id}&from={Q(from)}&to={Q(to)}&confirmationPageSize=101");

        tooBig.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetContext_DataOfAnotherTenant_IsNotVisible()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var tag = UniqueTag();
        var foreignMachine = await CreateMachineAsync(otherTenantClient, tag);
        var foreignOrder = await CreateReleasedOrderAsync(otherTenantClient, tag, "FRN");
        var foreignSignal = await RaiseAndonAsync(otherTenantClient, foreignMachine.Id);

        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var to = DateTime.UtcNow;
        var from = to.AddHours(-2);

        var tenantWide = await client.GetAsync($"{BaseUrl}?from={Q(from)}&to={Q(to)}");

        tenantWide.StatusCode.Should().Be(HttpStatusCode.OK);
        var context = await ReadAsync<ShiftHandoverContextDto>(tenantWide);
        context.OpenOrders.Select(o => o.Code).Should().NotContain(foreignOrder.Code);
        context.ActiveSignals.Select(s => s.Id).Should().NotContain(foreignSignal.Id);

        var foreignMachineScoped = await client.GetAsync(
            $"{BaseUrl}?machineId={foreignMachine.Id}&from={Q(from)}&to={Q(to)}");

        foreignMachineScoped.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static string Q(DateTime value) => Uri.EscapeDataString(value.ToString("o"));

    private static string UniqueTag() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    /// <summary>
    /// A single calendar entry guaranteed to cover <c>fromUtc</c>: same-day
    /// when the ±4h window fits, otherwise an overnight entry from the
    /// previous day or into the next day.
    /// </summary>
    private static async Task PutCalendarCoveringAsync(
        HttpClient client, Guid machineId, DateTime fromUtc, Guid? shiftId)
    {
        var t = fromUtc.TimeOfDay;
        var day = fromUtc.DayOfWeek;
        var start = t - TimeSpan.FromHours(4);
        var end = t + TimeSpan.FromHours(4);

        int entryDay;
        TimeSpan entryStart;
        TimeSpan entryEnd;
        if (start >= TimeSpan.Zero && end < TimeSpan.FromHours(24))
        {
            entryDay = (int)day;
            entryStart = start;
            entryEnd = end;
        }
        else if (end >= TimeSpan.FromHours(24))
        {
            entryDay = (int)day;
            entryStart = start;
            entryEnd = end - TimeSpan.FromHours(24);
        }
        else
        {
            entryDay = (int)(day == DayOfWeek.Sunday ? DayOfWeek.Saturday : day - 1);
            entryStart = start + TimeSpan.FromHours(24);
            entryEnd = end;
        }

        var response = await client.PutAsJsonAsync($"/api/machines/{machineId}/calendar", new
        {
            entries = new[]
            {
                new
                {
                    dayOfWeek = entryDay,
                    startTime = entryStart.ToString(@"hh\:mm\:ss"),
                    endTime = entryEnd.ToString(@"hh\:mm\:ss"),
                    shiftId,
                    isWorking = true
                }
            }
        });

        response.EnsureSuccessStatusCode();
    }

    private static async Task<MachineDto> CreateMachineAsync(HttpClient client, string tag)
    {
        var response = await client.PostAsJsonAsync("/api/machines", new
        {
            code = $"SH-{tag}-{Guid.NewGuid():N}"[..12],
            name = "Handover Center",
            description = (string?)null,
            isActive = true
        });

        response.EnsureSuccessStatusCode();
        return await ReadAsync<MachineDto>(response);
    }

    private static async Task<ShiftDto> CreateShiftAsync(HttpClient client, string tag)
    {
        var response = await client.PostAsJsonAsync("/api/shifts", new
        {
            code = $"SH-{tag}",
            name = $"Handover shift {tag}",
            description = (string?)null,
            startTime = "06:00:00",
            endTime = "14:00:00",
            isActive = true
        });

        response.EnsureSuccessStatusCode();
        return await ReadAsync<ShiftDto>(response);
    }

    private static async Task<ReasonCodeDto> CreateReasonCodeAsync(HttpClient client, string tag)
    {
        var response = await client.PostAsJsonAsync("/api/reason-codes", new
        {
            code = $"SH-{tag}",
            name = "Handover breakdown",
            description = (string?)null,
            category = 1,
            isActive = true,
            sortIndex = 0
        });

        response.EnsureSuccessStatusCode();
        return await ReadAsync<ReasonCodeDto>(response);
    }

    private sealed record OperatorSeed(Guid Id, string Identifier);

    private static async Task<OperatorSeed> CreateOperatorAsync(HttpClient client)
    {
        var identifier = $"SH-{Guid.NewGuid():N}"[..12];
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
        var created = await ReadAsync<OperatorDto>(response);
        return new OperatorSeed(created.Id, identifier);
    }

    private static async Task<ProductionOrderDto> CreateReleasedOrderAsync(
        HttpClient client, string tag, string suffix, int priority = 0)
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
            code = $"SH-{tag}-{suffix}",
            productId = Guid.NewGuid(),
            recipeId = recipe.Id,
            recipeVersionId = versionId,
            plannedQuantity = 100m,
            measureUnitId = (Guid?)null,
            priority,
            dueDate = (DateTime?)null,
            notes = (string?)null,
            syncId = (string?)null
        });
        orderResponse.EnsureSuccessStatusCode();
        var order = await ReadAsync<ProductionOrderDto>(orderResponse);

        var releaseOrderResponse = await client.PostAsync($"/api/production-orders/{order.Id}/release", null);
        releaseOrderResponse.EnsureSuccessStatusCode();
        return await ReadAsync<ProductionOrderDto>(releaseOrderResponse);
    }

    private static async Task<HttpResponseMessage> ConfirmAsync(
        HttpClient client,
        Guid orderId,
        Guid machineId,
        DateTime reportedAt,
        decimal good,
        decimal scrap,
        OperatorSeed? reporter)
    {
        return await client.PostAsJsonAsync("/api/production-confirmations", new
        {
            productionOrderId = orderId,
            machineId,
            reportedByOperatorId = reporter?.Id,
            reportedAt,
            goodQuantity = good,
            scrapQuantity = scrap,
            notes = (string?)null
        });
    }

    private static async Task<AndonSignalDto> RaiseAndonAsync(
        HttpClient client, Guid machineId, Guid? reasonCodeId = null, DateTime? raisedAt = null)
    {
        var response = await client.PostAsJsonAsync("/api/andon-signals", new
        {
            machineId,
            category = 1,
            reasonCodeId,
            raisedAt = raisedAt ?? DateTime.UtcNow.AddMinutes(-5),
            notes = "Handover jam",
            raisedByOperatorId = (Guid?)null,
            productionOrderId = (Guid?)null
        });

        response.EnsureSuccessStatusCode();
        return await ReadAsync<AndonSignalDto>(response);
    }

    private static async Task<RecipeDto> CreateRecipeAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/recipes", new
        {
            code = $"R-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            name = "Handover recipe",
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
