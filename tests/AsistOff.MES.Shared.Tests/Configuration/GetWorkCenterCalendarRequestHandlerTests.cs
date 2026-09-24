using AsistOff.MES.Configuration.Application.Features.WorkCenterCalendars.Get;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class GetWorkCenterCalendarRequestHandlerTests
{
    private readonly Mock<IMachinesRepository> _machines = new();
    private readonly Mock<IWorkCenterCalendarsRepository> _calendars = new();

    private readonly Machine _machine = new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        Code = "WC-1",
        Name = "Work Center 1"
    };

    [Fact]
    public async Task Handle_UnknownMachine_ThrowsNotFoundException()
    {
        var act = () => new GetWorkCenterCalendarRequestHandler(_machines.Object, _calendars.Object)
            .Handle(new GetWorkCenterCalendarRequest(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_MachineWithoutCalendar_ReturnsEmptyCalendar()
    {
        _machines
            .Setup(r => r.GetByIdAsync(_machine.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_machine);
        _calendars
            .Setup(r => r.GetByMachineIdAsync(_machine.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkCenterCalendar?)null);

        var result = await new GetWorkCenterCalendarRequestHandler(_machines.Object, _calendars.Object)
            .Handle(new GetWorkCenterCalendarRequest(_machine.Id), CancellationToken.None);

        result.Id.Should().Be(Guid.Empty);
        result.MachineId.Should().Be(_machine.Id);
        result.MachineCode.Should().Be("WC-1");
        result.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ExistingCalendar_ReturnsEntriesOrderedByDay()
    {
        _machines
            .Setup(r => r.GetByIdAsync(_machine.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_machine);

        var calendar = new WorkCenterCalendar
        {
            Id = Guid.NewGuid(),
            TenantId = _machine.TenantId,
            MachineId = _machine.Id
        };
        calendar.Entries.Add(new WorkCenterCalendarEntry
        {
            Id = Guid.NewGuid(),
            TenantId = _machine.TenantId,
            WorkCenterCalendarId = calendar.Id,
            DayOfWeek = DayOfWeek.Friday,
            StartTime = new TimeOnly(6, 0),
            EndTime = new TimeOnly(14, 0)
        });
        calendar.Entries.Add(new WorkCenterCalendarEntry
        {
            Id = Guid.NewGuid(),
            TenantId = _machine.TenantId,
            WorkCenterCalendarId = calendar.Id,
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeOnly(6, 0),
            EndTime = new TimeOnly(14, 0)
        });

        _calendars
            .Setup(r => r.GetByMachineIdAsync(_machine.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(calendar);

        var result = await new GetWorkCenterCalendarRequestHandler(_machines.Object, _calendars.Object)
            .Handle(new GetWorkCenterCalendarRequest(_machine.Id), CancellationToken.None);

        result.Id.Should().Be(calendar.Id);
        result.Entries.Select(e => e.DayOfWeek).Should().ContainInOrder(DayOfWeek.Monday, DayOfWeek.Friday);
    }
}
