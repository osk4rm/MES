using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for the weekly Work Center calendar
/// (<c>/api/machines/{id}/calendar</c>): read, atomic create-or-replace, overlap
/// and shift validation, tenant isolation and the database-level cascade when a
/// Work Center is deleted.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class WorkCenterCalendarEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string MachinesUrl = "/api/machines";
    private const string ShiftsUrl = "/api/shifts";

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync($"{MachinesUrl}/{Guid.NewGuid()}/calendar");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_UnknownMachine_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{MachinesUrl}/{Guid.NewGuid()}/calendar");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.PutAsJsonAsync($"{MachinesUrl}/{Guid.NewGuid()}/calendar", new
        {
            entries = Array.Empty<object>()
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_MachineWithoutCalendar_ReturnsEmptyCalendar()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var response = await client.GetAsync($"{MachinesUrl}/{machine.Id}/calendar");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var calendar = await ReadAsync<WorkCenterCalendarDto>(response);
        calendar.Id.Should().Be(Guid.Empty);
        calendar.MachineId.Should().Be(machine.Id);
        calendar.MachineCode.Should().Be(machine.Code);
        calendar.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Put_CreatesCalendar_AndIsRetrievable()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);
        var shift = await CreateShiftAsync(client);

        var response = await client.PutAsJsonAsync($"{MachinesUrl}/{machine.Id}/calendar", new
        {
            entries = new[]
            {
                Entry(DayOfWeek.Monday, "06:00:00", "14:00:00", shift.Id),
                Entry(DayOfWeek.Sunday, "22:00:00", "06:00:00", null)
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await ReadAsync<WorkCenterCalendarDto>(response);
        saved.Id.Should().NotBe(Guid.Empty);
        saved.Entries.Should().HaveCount(2);
        saved.Entries.Should().ContainSingle(e => e.ShiftId == shift.Id);

        var fetched = await ReadAsync<WorkCenterCalendarDto>(
            await client.GetAsync($"{MachinesUrl}/{machine.Id}/calendar"));

        fetched.Id.Should().Be(saved.Id);
        fetched.Entries.Should().HaveCount(2);
    }

    [Fact]
    public async Task Put_ExistingCalendar_ReplacesAllEntries()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var first = await ReadAsync<WorkCenterCalendarDto>(
            await client.PutAsJsonAsync($"{MachinesUrl}/{machine.Id}/calendar", new
            {
                entries = new[]
                {
                    Entry(DayOfWeek.Monday, "06:00:00", "14:00:00", null),
                    Entry(DayOfWeek.Tuesday, "06:00:00", "14:00:00", null)
                }
            }));

        var secondResponse = await client.PutAsJsonAsync($"{MachinesUrl}/{machine.Id}/calendar", new
        {
            entries = new[]
            {
                Entry(DayOfWeek.Wednesday, "08:00:00", "16:00:00", null)
            }
        });
        secondResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "replacing a calendar must succeed, body was: {0}",
            await secondResponse.Content.ReadAsStringAsync());
        var second = await ReadAsync<WorkCenterCalendarDto>(secondResponse);

        second.Id.Should().Be(first.Id);
        second.Entries.Should().ContainSingle(e => e.DayOfWeek == (int)DayOfWeek.Wednesday);

        var fetched = await ReadAsync<WorkCenterCalendarDto>(
            await client.GetAsync($"{MachinesUrl}/{machine.Id}/calendar"));

        fetched.Id.Should().Be(first.Id);
        fetched.Entries.Should().ContainSingle(e => e.DayOfWeek == (int)DayOfWeek.Wednesday);
    }

    [Fact]
    public async Task Put_OverlappingEntriesOnSameDay_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var response = await client.PutAsJsonAsync($"{MachinesUrl}/{machine.Id}/calendar", new
        {
            entries = new[]
            {
                Entry(DayOfWeek.Monday, "06:00:00", "14:00:00", null),
                Entry(DayOfWeek.Monday, "13:00:00", "22:00:00", null)
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_OvernightEntryOverlappingNextDay_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var response = await client.PutAsJsonAsync($"{MachinesUrl}/{machine.Id}/calendar", new
        {
            entries = new[]
            {
                Entry(DayOfWeek.Sunday, "22:00:00", "06:00:00", null),
                Entry(DayOfWeek.Monday, "02:00:00", "08:00:00", null)
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_OvernightEntryTouchingNextDay_Returns200()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var response = await client.PutAsJsonAsync($"{MachinesUrl}/{machine.Id}/calendar", new
        {
            entries = new[]
            {
                Entry(DayOfWeek.Sunday, "22:00:00", "06:00:00", null),
                Entry(DayOfWeek.Monday, "06:00:00", "14:00:00", null)
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await ReadAsync<WorkCenterCalendarDto>(response);
        saved.Entries.Should().HaveCount(2);
    }

    [Fact]
    public async Task Put_UnknownShiftId_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var response = await client.PutAsJsonAsync($"{MachinesUrl}/{machine.Id}/calendar", new
        {
            entries = new[]
            {
                Entry(DayOfWeek.Monday, "06:00:00", "14:00:00", Guid.NewGuid())
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_ShiftFromAnotherTenant_Returns400()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var foreignShift = await CreateShiftAsync(otherTenantClient);

        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);

        var response = await client.PutAsJsonAsync($"{MachinesUrl}/{machine.Id}/calendar", new
        {
            entries = new[] { Entry(DayOfWeek.Monday, "06:00:00", "14:00:00", foreignShift.Id) }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_UnknownMachine_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync($"{MachinesUrl}/{Guid.NewGuid()}/calendar", new
        {
            entries = new[] { Entry(DayOfWeek.Monday, "06:00:00", "14:00:00", null) }
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_CalendarOfMachineFromAnotherTenant_Returns404()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var foreignMachine = await CreateMachineAsync(otherTenantClient);

        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync($"{MachinesUrl}/{foreignMachine.Id}/calendar");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_Machine_CascadesCalendarAndEntries()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machine = await CreateMachineAsync(client);
        var calendar = await ReadAsync<WorkCenterCalendarDto>(
            await client.PutAsJsonAsync($"{MachinesUrl}/{machine.Id}/calendar", new
            {
                entries = new[]
                {
                    Entry(DayOfWeek.Monday, "06:00:00", "14:00:00", null),
                    Entry(DayOfWeek.Tuesday, "06:00:00", "14:00:00", null)
                }
            }));
        calendar.Entries.Should().HaveCount(2);

        var deleteResponse = await client.DeleteAsync($"{MachinesUrl}/{machine.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = Fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();

        var calendarRows = await context.Set<WorkCenterCalendar>()
            .IgnoreQueryFilters()
            .CountAsync(x => x.Id == calendar.Id);
        var entryRows = await context.Set<WorkCenterCalendarEntry>()
            .IgnoreQueryFilters()
            .CountAsync(x => x.WorkCenterCalendarId == calendar.Id);

        calendarRows.Should().Be(0);
        entryRows.Should().Be(0);
    }

    private static object Entry(DayOfWeek day, string startTime, string endTime, Guid? shiftId) => new
    {
        dayOfWeek = (int)day,
        startTime,
        endTime,
        shiftId,
        isWorking = true
    };

    private static async Task<MachineDto> CreateMachineAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(MachinesUrl, new
        {
            code = UniqueCode(),
            name = "Work Center",
            description = (string?)null,
            isActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<MachineDto>(response);
    }

    private static async Task<ShiftDto> CreateShiftAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(ShiftsUrl, new
        {
            code = UniqueCode(),
            name = "Morning",
            description = (string?)null,
            startTime = "06:00:00",
            endTime = "14:00:00",
            isActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadAsync<ShiftDto>(response);
    }

    private static string UniqueCode() => $"IT-{Guid.NewGuid():N}"[..12];
}
