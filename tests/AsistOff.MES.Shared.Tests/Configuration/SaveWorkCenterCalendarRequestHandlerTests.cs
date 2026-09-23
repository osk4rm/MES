using AsistOff.MES.Configuration.Application.Features.WorkCenterCalendar.Save;
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
    private readonly Mock<IShiftsRepository> _shifts = new();
    private readonly Mock<IWorkCenterCalendarsRepository> _calendars = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<ITenantContext> _tenant = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _machineId = Guid.NewGuid();

    public SaveWorkCenterCalendarRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
        _machines
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Machine { Id = _machineId, Code = "M1", Name = "Machine" });
        _calendars
            .Setup(r => r.GetByMachineIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkCenterCalendar?)null);
    }

    private SaveWorkCenterCalendarRequestHandler CreateSut() =>
        new(_machines.Object, _shifts.Object, _calendars.Object, _guids.Object, _tenant.Object);

    private static WorkCenterCalendarEntryInput Entry(
        DayOfWeek day, string start, string end, Guid? shiftId = null, bool isWorking = true) =>
        new(day, TimeOnly.Parse(start), TimeOnly.Parse(end), shiftId, isWorking);

    [Fact]
    public async Task Handle_UnknownMachine_ThrowsNotFoundException()
    {
        // Arrange
        _machines
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Machine?)null);

        var request = new SaveWorkCenterCalendarRequest { MachineId = Guid.NewGuid() };

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ValidEntries_CreatesCalendarAndReplacesEntries()
    {
        // Arrange
        WorkCenterCalendar? created = null;
        _calendars
            .Setup(r => r.AddAsync(It.IsAny<WorkCenterCalendar>(), It.IsAny<CancellationToken>()))
            .Callback<WorkCenterCalendar, CancellationToken>((calendar, _) => created = calendar)
            .ReturnsAsync((WorkCenterCalendar calendar, CancellationToken _) => calendar);

        IReadOnlyCollection<WorkCenterCalendarEntry>? replaced = null;
        _calendars
            .Setup(r => r.ReplaceEntriesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<WorkCenterCalendarEntry>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyCollection<WorkCenterCalendarEntry>, CancellationToken>((_, entries, _) => replaced = entries)
            .Returns(Task.CompletedTask);

        var request = new SaveWorkCenterCalendarRequest
        {
            MachineId = _machineId,
            Entries = [Entry(DayOfWeek.Monday, "06:00", "14:00")]
        };

        // Act
        await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        created.Should().NotBeNull();
        created!.TenantId.Should().Be(_tenantId);
        created.MachineId.Should().Be(_machineId);
        replaced.Should().NotBeNull();
        replaced.Should().ContainSingle();
        replaced!.Single().TenantId.Should().Be(_tenantId);
        replaced.Single().CalendarId.Should().Be(created.Id);
        replaced.Single().DayOfWeek.Should().Be(DayOfWeek.Monday);
        replaced.Single().StartTime.Should().Be(new TimeOnly(6, 0));
    }

    [Fact]
    public async Task Handle_ExistingCalendar_ReplacesEntriesWithoutCreating()
    {
        // Arrange
        var existing = new WorkCenterCalendar { Id = Guid.NewGuid(), TenantId = _tenantId, MachineId = _machineId };
        _calendars
            .Setup(r => r.GetByMachineIdAsync(_machineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var request = new SaveWorkCenterCalendarRequest
        {
            MachineId = _machineId,
            Entries = [Entry(DayOfWeek.Tuesday, "14:00", "22:00")]
        };

        // Act
        await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        _calendars.Verify(r => r.AddAsync(It.IsAny<WorkCenterCalendar>(), It.IsAny<CancellationToken>()), Times.Never);
        _calendars.Verify(r => r.ReplaceEntriesAsync(
            existing.Id,
            It.Is<IReadOnlyCollection<WorkCenterCalendarEntry>>(e => e.Count == 1 && e.Single().DayOfWeek == DayOfWeek.Tuesday),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OverlappingEntries_ThrowsValidationException()
    {
        // Arrange
        var request = new SaveWorkCenterCalendarRequest
        {
            MachineId = _machineId,
            Entries =
            [
                Entry(DayOfWeek.Monday, "06:00", "14:00"),
                Entry(DayOfWeek.Monday, "13:00", "22:00")
            ]
        };

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _calendars.Verify(r => r.ReplaceEntriesAsync(
            It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<WorkCenterCalendarEntry>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OvernightEntryOverlapping_ThrowsValidationException()
    {
        // Arrange
        // 22:00-06:00 crosses midnight; 05:00-07:00 intersects its [00:00, 06:00) tail.
        var request = new SaveWorkCenterCalendarRequest
        {
            MachineId = _machineId,
            Entries =
            [
                Entry(DayOfWeek.Monday, "22:00", "06:00"),
                Entry(DayOfWeek.Monday, "05:00", "07:00")
            ]
        };

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_OvernightEntryNonOverlapping_IsAccepted()
    {
        // Arrange
        var request = new SaveWorkCenterCalendarRequest
        {
            MachineId = _machineId,
            Entries =
            [
                Entry(DayOfWeek.Monday, "22:00", "06:00"),
                Entry(DayOfWeek.Monday, "06:00", "14:00")
            ]
        };

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_EntriesOnDifferentDays_DoNotConflict()
    {
        // Arrange
        var request = new SaveWorkCenterCalendarRequest
        {
            MachineId = _machineId,
            Entries =
            [
                Entry(DayOfWeek.Monday, "06:00", "14:00"),
                Entry(DayOfWeek.Tuesday, "06:00", "14:00")
            ]
        };

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_UnknownShiftId_ThrowsValidationException()
    {
        // Arrange
        _shifts
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shift?)null);

        var request = new SaveWorkCenterCalendarRequest
        {
            MachineId = _machineId,
            Entries = [Entry(DayOfWeek.Monday, "06:00", "14:00", Guid.NewGuid())]
        };

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_KnownShiftId_IsAccepted()
    {
        // Arrange
        var shiftId = Guid.NewGuid();
        _shifts
            .Setup(r => r.GetByIdAsync(shiftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Shift { Id = shiftId, Code = "S1", Name = "Morning" });

        var request = new SaveWorkCenterCalendarRequest
        {
            MachineId = _machineId,
            Entries = [Entry(DayOfWeek.Monday, "06:00", "14:00", shiftId)]
        };

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_InvalidDayOfWeek_ThrowsValidationException()
    {
        // Arrange
        var request = new SaveWorkCenterCalendarRequest
        {
            MachineId = _machineId,
            Entries = [new WorkCenterCalendarEntryInput((DayOfWeek)7, new TimeOnly(6, 0), new TimeOnly(14, 0), null, true)]
        };

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }
}
