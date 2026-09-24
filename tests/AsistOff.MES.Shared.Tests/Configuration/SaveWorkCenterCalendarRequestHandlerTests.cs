using AsistOff.MES.Configuration.Application.Features.WorkCenterCalendars.Save;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class SaveWorkCenterCalendarRequestHandlerTests
{
    private readonly Mock<IMachinesRepository> _machines = new();
    private readonly Mock<IWorkCenterCalendarsRepository> _calendars = new();
    private readonly Mock<IShiftsRepository> _shifts = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<ITenantContext> _tenant = new();

    private readonly Machine _machine = new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        Code = "WC-1",
        Name = "Work Center 1"
    };

    public SaveWorkCenterCalendarRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _tenant.SetupGet(t => t.TenantId).Returns(Guid.NewGuid());

        _machines
            .Setup(r => r.GetByIdAsync(_machine.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_machine);

        _shifts
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                (IReadOnlyCollection<Shift>)ids.Select(id => new Shift
                {
                    Id = id,
                    TenantId = Guid.NewGuid(),
                    Code = "S1",
                    Name = "Morning",
                    StartTime = new TimeOnly(6, 0),
                    EndTime = new TimeOnly(14, 0)
                }).ToList());
    }

    private SaveWorkCenterCalendarRequestHandler CreateSut() =>
        new(_machines.Object, _calendars.Object, _shifts.Object, _guids.Object, _tenant.Object);

    [Fact]
    public async Task Handle_UnknownMachine_ThrowsNotFoundException()
    {
        var request = new SaveWorkCenterCalendarRequest(Guid.NewGuid(), []);

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_OverlappingEntriesOnSameDay_ThrowsValidationException()
    {
        var request = Request(
            Entry(DayOfWeek.Monday, 6, 0, 14, 0),
            Entry(DayOfWeek.Monday, 13, 0, 22, 0));

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_OvernightEntryOverlappingNextDayEntry_ThrowsValidationException()
    {
        // Sunday 22:00 -> Monday 06:00 runs into Monday 02:00 -> 08:00.
        var request = Request(
            Entry(DayOfWeek.Sunday, 22, 0, 6, 0),
            Entry(DayOfWeek.Monday, 2, 0, 8, 0));

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_OvernightEntryTouchingNextDayEntry_IsAccepted()
    {
        // Sunday 22:00 -> Monday 06:00 ends exactly when Monday 06:00 starts.
        var request = Request(
            Entry(DayOfWeek.Sunday, 22, 0, 6, 0),
            Entry(DayOfWeek.Monday, 6, 0, 14, 0));

        var result = await CreateSut().Handle(request, CancellationToken.None);

        result.Entries.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_UnknownShiftId_ThrowsValidationException()
    {
        var knownShift = Guid.NewGuid();
        _shifts
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                (IReadOnlyCollection<Shift>)ids
                    .Where(id => id == knownShift)
                    .Select(id => new Shift
                    {
                        Id = id,
                        TenantId = Guid.NewGuid(),
                        Code = "S1",
                        Name = "Morning",
                        StartTime = new TimeOnly(6, 0),
                        EndTime = new TimeOnly(14, 0)
                    })
                    .ToList());

        var request = Request(Entry(DayOfWeek.Monday, 6, 0, 14, 0, Guid.NewGuid()));

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_InvalidDayOfWeek_ThrowsValidationException()
    {
        var request = Request(Entry((DayOfWeek)42, 6, 0, 14, 0));

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_NoExistingCalendar_CreatesCalendarWithEntries()
    {
        var tenantId = Guid.NewGuid();
        _tenant.SetupGet(t => t.TenantId).Returns(tenantId);
        _calendars
            .Setup(r => r.GetByMachineIdAsync(_machine.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkCenterCalendar?)null);

        WorkCenterCalendar? created = null;
        _calendars
            .Setup(r => r.CreateAsync(It.IsAny<WorkCenterCalendar>(), It.IsAny<CancellationToken>()))
            .Callback<WorkCenterCalendar, CancellationToken>((c, _) => created = c)
            .ReturnsAsync((WorkCenterCalendar c, CancellationToken _) => c);

        var request = Request(Entry(DayOfWeek.Monday, 6, 0, 14, 0));

        var result = await CreateSut().Handle(request, CancellationToken.None);

        created.Should().NotBeNull();
        created!.MachineId.Should().Be(_machine.Id);
        created.TenantId.Should().Be(tenantId);
        created.Entries.Should().ContainSingle();
        result.Id.Should().Be(created.Id);
        result.MachineId.Should().Be(_machine.Id);
        result.MachineCode.Should().Be("WC-1");
        result.Entries.Should().ContainSingle();
        _calendars.Verify(
            r => r.ReplaceEntriesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<WorkCenterCalendarEntry>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ExistingCalendar_ReplacesEntriesAtomically()
    {
        var existing = new WorkCenterCalendar
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            MachineId = _machine.Id
        };
        existing.Entries.Add(new WorkCenterCalendarEntry
        {
            Id = Guid.NewGuid(),
            TenantId = existing.TenantId,
            WorkCenterCalendarId = existing.Id,
            DayOfWeek = DayOfWeek.Friday,
            StartTime = new TimeOnly(0, 0),
            EndTime = new TimeOnly(6, 0)
        });

        _calendars
            .Setup(r => r.GetByMachineIdAsync(_machine.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        IReadOnlyCollection<WorkCenterCalendarEntry>? replaced = null;
        _calendars
            .Setup(r => r.ReplaceEntriesAsync(existing.Id, It.IsAny<IReadOnlyCollection<WorkCenterCalendarEntry>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyCollection<WorkCenterCalendarEntry>, CancellationToken>((_, entries, _) => replaced = entries)
            .Returns(Task.CompletedTask);

        var request = Request(Entry(DayOfWeek.Tuesday, 6, 0, 14, 0));

        var result = await CreateSut().Handle(request, CancellationToken.None);

        result.Id.Should().Be(existing.Id);
        replaced.Should().NotBeNull();
        replaced!.Should().ContainSingle(e => e.DayOfWeek == DayOfWeek.Tuesday);
        _calendars.Verify(
            r => r.CreateAsync(It.IsAny<WorkCenterCalendar>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_EntriesAreReturnedSortedByDayThenStartTime()
    {
        _calendars
            .Setup(r => r.GetByMachineIdAsync(_machine.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkCenterCalendar?)null);

        var request = Request(
            Entry(DayOfWeek.Wednesday, 14, 0, 22, 0),
            Entry(DayOfWeek.Monday, 14, 0, 22, 0),
            Entry(DayOfWeek.Monday, 6, 0, 14, 0));

        var result = await CreateSut().Handle(request, CancellationToken.None);

        result.Entries.Select(e => (e.DayOfWeek, e.StartTime)).Should().BeEquivalentTo(
            new[]
            {
                (DayOfWeek.Monday, new TimeOnly(6, 0)),
                (DayOfWeek.Monday, new TimeOnly(14, 0)),
                (DayOfWeek.Wednesday, new TimeOnly(14, 0))
            },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Handle_LinkedShiftIsPersisted()
    {
        var shiftId = Guid.NewGuid();
        _calendars
            .Setup(r => r.GetByMachineIdAsync(_machine.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkCenterCalendar?)null);

        var request = Request(Entry(DayOfWeek.Monday, 6, 0, 14, 0, shiftId));

        var result = await CreateSut().Handle(request, CancellationToken.None);

        result.Entries.Should().ContainSingle(e => e.ShiftId == shiftId);
    }

    private SaveWorkCenterCalendarRequest Request(params WorkCenterCalendarEntryRequest[] entries) =>
        new(_machine.Id, entries);

    private static WorkCenterCalendarEntryRequest Entry(
        DayOfWeek day, int startHour, int startMinute, int endHour, int endMinute, Guid? shiftId = null) =>
        new(day, new TimeOnly(startHour, startMinute), new TimeOnly(endHour, endMinute), shiftId, true);
}
