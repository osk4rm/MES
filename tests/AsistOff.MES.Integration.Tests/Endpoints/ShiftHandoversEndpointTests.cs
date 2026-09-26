using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for the persisted shift handover logbook
/// (slice 2/2, issue #293): <c>POST /api/shift-handovers</c>,
/// <c>GET /api/shift-handovers</c> and <c>GET /api/shift-handovers/{id}</c>.
/// They exercise the full request pipeline (auth, tenant resolution,
/// validation, shift-window resolution, snapshot counts and the global
/// exception handler) against a real PostgreSQL database. The database is
/// shared with every other integration test class, so assertions only
/// constrain the rows seeded here.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ShiftHandoversEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/shift-handovers";

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();
        var to = DateTime.UtcNow;
        var from = to.AddHours(-8);

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            machineId = Guid.NewGuid(),
            from,
            to,
            notes = "Night shift notes."
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_EmptyNotes_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client, UniqueTag());
        var to = DateTime.UtcNow;
        var from = to.AddHours(-8);

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            machineId = machine.Id,
            from,
            to,
            notes = "  "
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_ReversedWindow_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client, UniqueTag());
        var to = DateTime.UtcNow;

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            machineId = machine.Id,
            from = to,
            to = to.AddHours(-8),
            notes = "Notes."
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WindowOver24Hours_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client, UniqueTag());
        var to = DateTime.UtcNow;

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            machineId = machine.Id,
            from = to.AddHours(-25),
            to,
            notes = "Notes."
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_UnknownMachine_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var to = DateTime.UtcNow;

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            machineId = Guid.NewGuid(),
            from = to.AddHours(-8),
            to,
            notes = "Notes."
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_HappyPath_Returns201_AndGetRoundTripsWithShiftAndSnapshots()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = UniqueTag();
        var machine = await CreateMachineAsync(client, tag);
        var shift = await CreateShiftAsync(client, tag);
        var order = await CreateReleasedOrderAsync(client, tag, "HND");
        var signal = await RaiseAndonAsync(client, machine.Id);

        var to = DateTime.UtcNow;
        var from = to.AddHours(-8);
        await PutCalendarCoveringAsync(client, machine.Id, from, shift.Id);

        var create = await client.PostAsJsonAsync(BaseUrl, new
        {
            machineId = machine.Id,
            from,
            to,
            notes = "Bearing replaced, watch vibration."
        });

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ShiftHandoverDto>(create);
        created.MachineId.Should().Be(machine.Id);
        created.ShiftId.Should().Be(shift.Id);
        created.UncoveredShift.Should().BeFalse();
        created.Notes.Should().Be("Bearing replaced, watch vibration.");
        created.From.Should().BeCloseTo(from.ToUniversalTime(), TimeSpan.FromSeconds(1));
        created.To.Should().BeCloseTo(to.ToUniversalTime(), TimeSpan.FromSeconds(1));
        created.CreatedByUserId.Should().NotBeNull();
        created.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(2));
        // The shared database holds other tests' rows, so only lower bounds
        // are asserted: the seeded open order and active signal must be counted.
        created.OpenOrdersCount.Should().BeGreaterThanOrEqualTo(1);
        created.ActiveAndonCount.Should().BeGreaterThanOrEqualTo(1);

        var get = await client.GetAsync($"{BaseUrl}/{created.Id}");

        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadAsync<ShiftHandoverDto>(get);
        // PostgreSQL timestamp truncates sub-microsecond ticks, so compare
        // instants with tolerance instead of strict record equivalence.
        fetched.Id.Should().Be(created.Id);
        fetched.MachineId.Should().Be(created.MachineId);
        fetched.ShiftId.Should().Be(created.ShiftId);
        fetched.Notes.Should().Be(created.Notes);
        fetched.From.Should().BeCloseTo(created.From, TimeSpan.FromMilliseconds(10));
        fetched.To.Should().BeCloseTo(created.To, TimeSpan.FromMilliseconds(10));
        fetched.CreatedByUserId.Should().Be(created.CreatedByUserId);
        fetched.CreatedAt.Should().BeCloseTo(created.CreatedAt, TimeSpan.FromMilliseconds(10));
        fetched.OpenOrdersCount.Should().Be(created.OpenOrdersCount);
        fetched.ActiveAndonCount.Should().Be(created.ActiveAndonCount);
        fetched.UncoveredShift.Should().Be(created.UncoveredShift);
    }

    [Fact]
    public async Task Create_MachineWithoutCalendar_PersistsUncoveredShift()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client, UniqueTag());
        var to = DateTime.UtcNow;
        var from = to.AddHours(-8);

        var create = await client.PostAsJsonAsync(BaseUrl, new
        {
            machineId = machine.Id,
            from,
            to,
            notes = "No calendar on this machine."
        });

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ShiftHandoverDto>(create);
        created.ShiftId.Should().BeNull();
        created.UncoveredShift.Should().BeTrue();
    }

    [Fact]
    public async Task Create_DuplicateBoundary_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client, UniqueTag());
        var to = DateTime.UtcNow;
        var from = to.AddHours(-8);

        var first = await client.PostAsJsonAsync(BaseUrl, new
        {
            machineId = machine.Id,
            from,
            to,
            notes = "First note."
        });
        first.EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync(BaseUrl, new
        {
            machineId = machine.Id,
            from,
            to,
            notes = "Second note for the same boundary."
        });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Browse_FiltersByMachineAndRange_NewestFirst_Paged()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = UniqueTag();
        var machine = await CreateMachineAsync(client, tag);
        var otherMachine = await CreateMachineAsync(client, tag);
        var anchor = DateTime.UtcNow.AddDays(-2).Date.AddHours(12);

        var first = await CreateHandoverAsync(client, machine.Id, anchor.AddHours(-16), anchor.AddHours(-8), "First.");
        var second = await CreateHandoverAsync(client, machine.Id, anchor.AddHours(-8), anchor, "Second.");
        await CreateHandoverAsync(client, otherMachine.Id, anchor.AddHours(-8), anchor, "Other machine.");

        var from = anchor.AddDays(-1);
        var to = anchor.AddDays(1);
        var browse = await client.GetAsync(
            $"{BaseUrl}?machineId={machine.Id}&from={Q(from)}&to={Q(to)}&pageNumber=1&pageSize=20");

        browse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<ShiftHandoverDto>>(browse);
        page.Items.Select(i => i.Id).Should().ContainInOrder(second.Id, first.Id);
        page.Items.Should().OnlyContain(i => i.MachineId == machine.Id);
        page.TotalCount.Should().BeGreaterThanOrEqualTo(2);

        var secondPage = await client.GetAsync(
            $"{BaseUrl}?machineId={machine.Id}&from={Q(from)}&to={Q(to)}&pageNumber=2&pageSize=1");

        secondPage.StatusCode.Should().Be(HttpStatusCode.OK);
        var page2 = await ReadAsync<PagedResponseDto<ShiftHandoverDto>>(secondPage);
        page2.Items.Should().ContainSingle().Which.Id.Should().Be(first.Id);

        var tooBig = await client.GetAsync($"{BaseUrl}?pageSize=101");

        tooBig.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Browse_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task History_IsAppendOnly_PutAndDeleteReturn405()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client, UniqueTag());
        var to = DateTime.UtcNow;
        var created = await CreateHandoverAsync(client, machine.Id, to.AddHours(-8), to, "Immutable.");

        var put = await client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", new { notes = "Edited." });
        var delete = await client.DeleteAsync($"{BaseUrl}/{created.Id}");

        // No PUT/DELETE routes exist: the path matches the GET-by-id route, so
        // the framework answers 405 rather than 404.
        put.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        delete.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);

        // The entry is untouched.
        var get = await client.GetAsync($"{BaseUrl}/{created.Id}");
        (await ReadAsync<ShiftHandoverDto>(get)).Notes.Should().Be("Immutable.");
    }

    [Fact]
    public async Task DataOfAnotherTenant_IsNotVisible()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var foreignMachine = await CreateMachineAsync(otherTenantClient, UniqueTag());
        var to = DateTime.UtcNow;
        var foreign = await CreateHandoverAsync(
            otherTenantClient, foreignMachine.Id, to.AddHours(-8), to, "Foreign notes.");

        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var browse = await client.GetAsync($"{BaseUrl}?pageNumber=1&pageSize=100");
        browse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<ShiftHandoverDto>>(browse);
        page.Items.Select(i => i.Id).Should().NotContain(foreign.Id);

        var foreignMachineScoped = await client.GetAsync(
            $"{BaseUrl}?machineId={foreignMachine.Id}&pageNumber=1&pageSize=20");
        foreignMachineScoped.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<PagedResponseDto<ShiftHandoverDto>>(foreignMachineScoped))
            .Items.Should().BeEmpty();

        var directGet = await client.GetAsync($"{BaseUrl}/{foreign.Id}");
        directGet.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static string Q(DateTime value) => Uri.EscapeDataString(value.ToString("o"));

    private static string UniqueTag() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static async Task<ShiftHandoverDto> CreateHandoverAsync(
        HttpClient client, Guid machineId, DateTime from, DateTime to, string notes)
    {
        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            machineId,
            from,
            to,
            notes
        });

        response.EnsureSuccessStatusCode();
        return await ReadAsync<ShiftHandoverDto>(response);
    }

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
            code = $"HO-{tag}-{Guid.NewGuid():N}"[..12],
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
            code = $"HO-{tag}",
            name = $"Handover shift {tag}",
            description = (string?)null,
            startTime = "06:00:00",
            endTime = "14:00:00",
            isActive = true
        });

        response.EnsureSuccessStatusCode();
        return await ReadAsync<ShiftDto>(response);
    }

    private static async Task<ProductionOrderDto> CreateReleasedOrderAsync(
        HttpClient client, string tag, string suffix)
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
            code = $"HO-{tag}-{suffix}",
            productId = Guid.NewGuid(),
            recipeId = recipe.Id,
            recipeVersionId = versionId,
            plannedQuantity = 100m,
            measureUnitId = (Guid?)null,
            priority = 0,
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

    private static async Task<AndonSignalDto> RaiseAndonAsync(HttpClient client, Guid machineId)
    {
        var response = await client.PostAsJsonAsync("/api/andon-signals", new
        {
            machineId,
            category = 1,
            reasonCodeId = (Guid?)null,
            raisedAt = DateTime.UtcNow.AddMinutes(-5),
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
}
