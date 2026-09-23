using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for
/// <c>/api/machines/{machineId}/calendar</c>. They exercise the full request
/// pipeline: authentication, tenant resolution, MediatR handler, EF Core
/// persistence and the global exception handler - against a real PostgreSQL
/// database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class WorkCenterCalendarsEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task GetCalendar_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync($"/api/machines/{Guid.NewGuid()}/calendar");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCalendar_WithUnknownMachine_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/api/machines/{Guid.NewGuid()}/calendar");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SaveCalendar_WithUnknownMachine_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync(
            $"/api/machines/{Guid.NewGuid()}/calendar",
            new { entries = Array.Empty<object>() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SaveCalendar_CreatesEntries_AndGetReturnsThem()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);

        var save = await client.PutAsJsonAsync($"/api/machines/{machineId}/calendar", new
        {
            entries = new[]
            {
                new { dayOfWeek = 1, startTime = "06:00:00", endTime = "14:00:00", shiftId = (Guid?)null, isWorking = true },
                new { dayOfWeek = 2, startTime = "14:00:00", endTime = "22:00:00", shiftId = (Guid?)null, isWorking = true }
            }
        });

        save.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await ReadAsync<WorkCenterCalendarDto>(save);
        saved.MachineId.Should().Be(machineId);
        saved.Entries.Should().HaveCount(2);

        var get = await client.GetAsync($"/api/machines/{machineId}/calendar");

        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadAsync<WorkCenterCalendarDto>(get);
        fetched.Id.Should().Be(saved.Id);
        fetched.Entries.Should().HaveCount(2);
        fetched.Entries.Should().ContainSingle(e =>
            e.DayOfWeek == DayOfWeek.Monday && e.StartTime == new TimeOnly(6, 0) && e.IsWorking);
    }

    [Fact]
    public async Task SaveCalendar_ReplacesExistingEntriesAtomically()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        await client.PutAsJsonAsync($"/api/machines/{machineId}/calendar", new
        {
            entries = new[]
            {
                new { dayOfWeek = 1, startTime = "06:00:00", endTime = "14:00:00", shiftId = (Guid?)null, isWorking = true },
                new { dayOfWeek = 2, startTime = "06:00:00", endTime = "14:00:00", shiftId = (Guid?)null, isWorking = true }
            }
        });

        var replace = await client.PutAsJsonAsync($"/api/machines/{machineId}/calendar", new
        {
            entries = new[]
            {
                new { dayOfWeek = 3, startTime = "22:00:00", endTime = "06:00:00", shiftId = (Guid?)null, isWorking = true }
            }
        });

        replace.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await ReadAsync<WorkCenterCalendarDto>(replace);
        saved.Entries.Should().ContainSingle();
        saved.Entries.Single().DayOfWeek.Should().Be(DayOfWeek.Wednesday);
    }

    [Fact]
    public async Task SaveCalendar_WithOverlappingEntries_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);

        var response = await client.PutAsJsonAsync($"/api/machines/{machineId}/calendar", new
        {
            entries = new[]
            {
                new { dayOfWeek = 1, startTime = "06:00:00", endTime = "14:00:00", shiftId = (Guid?)null, isWorking = true },
                new { dayOfWeek = 1, startTime = "13:00:00", endTime = "22:00:00", shiftId = (Guid?)null, isWorking = true }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SaveCalendar_WithOvernightOverlap_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);

        // 22:00-06:00 crosses midnight; 05:00-07:00 intersects its post-midnight tail.
        var response = await client.PutAsJsonAsync($"/api/machines/{machineId}/calendar", new
        {
            entries = new[]
            {
                new { dayOfWeek = 1, startTime = "22:00:00", endTime = "06:00:00", shiftId = (Guid?)null, isWorking = true },
                new { dayOfWeek = 1, startTime = "05:00:00", endTime = "07:00:00", shiftId = (Guid?)null, isWorking = true }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SaveCalendar_WithUnknownShiftId_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);

        var response = await client.PutAsJsonAsync($"/api/machines/{machineId}/calendar", new
        {
            entries = new[]
            {
                new { dayOfWeek = 1, startTime = "06:00:00", endTime = "14:00:00", shiftId = (Guid?)Guid.NewGuid(), isWorking = true }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SaveCalendar_WithKnownShiftId_PersistsReference()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var shift = await ReadAsync<ShiftDto>(await client.PostAsJsonAsync("/api/shifts",
            new { code = UniqueCode(), name = "Morning", description = (string?)null, startTime = "06:00:00", endTime = "14:00:00", isActive = true }));

        var response = await client.PutAsJsonAsync($"/api/machines/{machineId}/calendar", new
        {
            entries = new[]
            {
                new { dayOfWeek = 1, startTime = "06:00:00", endTime = "14:00:00", shiftId = (Guid?)shift.Id, isWorking = true }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await ReadAsync<WorkCenterCalendarDto>(response);
        saved.Entries.Should().ContainSingle(e => e.ShiftId == shift.Id && e.ShiftCode == shift.Code);
    }

    [Fact]
    public async Task DeleteMachine_CascadesToCalendar()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var save = await client.PutAsJsonAsync($"/api/machines/{machineId}/calendar", new
        {
            entries = new[]
            {
                new { dayOfWeek = 1, startTime = "06:00:00", endTime = "14:00:00", shiftId = (Guid?)null, isWorking = true }
            }
        });
        save.StatusCode.Should().Be(HttpStatusCode.OK);

        var delete = await client.DeleteAsync($"/api/machines/{machineId}");

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Both the machine and its calendar (with entries) are gone.
        (await client.GetAsync($"/api/machines/{machineId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/machines/{machineId}/calendar")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<Guid> CreateMachineAsync(HttpClient client)
    {
        var code = $"IT-{Guid.NewGuid():N}"[..12];
        var response = await client.PostAsJsonAsync("/api/machines", new
        {
            code,
            name = "Calendar machine",
            description = (string?)null,
            isActive = true,
            departmentId = (Guid?)null,
            syncId = (string?)null
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await ReadAsync<MachineDto>(response)).Id;
    }

    private static string UniqueCode() => $"IT-{Guid.NewGuid():N}"[..12];
}
