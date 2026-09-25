using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Oee.Summary;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class GetOeeSummaryRequestHandlerTests
{
    private static readonly DateTime Monday = new(2026, 9, 7, 6, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IMachinesRepository> _machines = new();
    private readonly Mock<IWorkCenterCalendarsRepository> _calendars = new();
    private readonly Mock<IDowntimeEventsRepository> _downtimes = new();
    private readonly Mock<IProductionConfirmationsRepository> _confirmations = new();

    public GetOeeSummaryRequestHandlerTests()
    {
        // Defaults: no calendar (zero planned time) and no downtime, so the
        // slice-1 quality tests exercise the empty-availability path.
        _calendars.Setup(c => c.GetByMachineIdAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkCenterCalendar?)null);
        _downtimes.Setup(d => d.ListOverlappingAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DowntimeEvent>());
    }

    private GetOeeSummaryRequestHandler CreateSut() => new(
        _machines.Object, _calendars.Object, _downtimes.Object, _confirmations.Object);

    private static Machine AMachine(Guid id) => new()
    {
        Id = id,
        TenantId = Guid.NewGuid(),
        Code = "WC-1",
        Name = "Work Center 1"
    };

    private static ProductionConfirmation Confirmation(
        Guid machineId, DateTime reportedAt, decimal good, decimal scrap) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        ProductionOrderId = Guid.NewGuid(),
        MachineId = machineId,
        ReportedAt = reportedAt,
        GoodQuantity = good,
        ScrapQuantity = scrap
    };

    private void ArrangeMachine(Guid machineId)
    {
        _machines.Setup(m => m.GetByIdAsync(machineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AMachine(machineId));
    }

    private static WorkCenterCalendarEntry WorkingEntry(DayOfWeek day, string start, string end) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        WorkCenterCalendarId = Guid.NewGuid(),
        DayOfWeek = day,
        StartTime = TimeOnly.Parse(start),
        EndTime = TimeOnly.Parse(end),
        IsWorking = true
    };

    private void ArrangeCalendar(params WorkCenterCalendarEntry[] entries)
    {
        _calendars.Setup(c => c.GetByMachineIdAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkCenterCalendar
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                MachineId = Guid.NewGuid(),
                Entries = entries.ToList()
            });
    }

    private static DowntimeEvent ClosedDowntime(Guid machineId, DateTime startedAt, DateTime endedAt) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        MachineId = machineId,
        ReasonCodeId = Guid.NewGuid(),
        StartedAt = startedAt,
        EndedAt = endedAt
    };

    private static DowntimeEvent OpenDowntime(Guid machineId, DateTime startedAt) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        MachineId = machineId,
        ReasonCodeId = Guid.NewGuid(),
        StartedAt = startedAt,
        EndedAt = null
    };

    private void ArrangeDowntimes(params DowntimeEvent[] events)
    {
        _downtimes.Setup(d => d.ListOverlappingAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);
    }

    [Fact]
    public void Request_ImplementsTenantRequest_AndNotAnonymous()
    {
        // Assert - the summary query must stay tenant-scoped
        typeof(ITenantRequest<OeeSummaryResponse>)
            .IsAssignableFrom(typeof(GetOeeSummaryRequest)).Should().BeTrue();
        typeof(IAllowAnonymousRequest).IsAssignableFrom(typeof(GetOeeSummaryRequest)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_HappyPath_ReturnsCountsAndQuality()
    {
        // Arrange - 90 good + 10 scrap across two rows: quality = 0.9
        var machineId = Guid.NewGuid();
        var from = Monday;
        var to = Monday.AddHours(8);
        ArrangeMachine(machineId);
        _confirmations.Setup(c => c.ListForMachineInWindowAsync(
                machineId, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Confirmation(machineId, Monday.AddHours(2), 50m, 5m),
                Confirmation(machineId, Monday.AddHours(3), 40m, 5m)]);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeSummaryRequest(machineId, from, to), CancellationToken.None);

        // Assert
        result.MachineId.Should().Be(machineId);
        result.FromUtc.Should().Be(from);
        result.ToUtc.Should().Be(to);
        result.GoodCount.Should().Be(90m);
        result.ScrapCount.Should().Be(10m);
        result.TotalCount.Should().Be(100m);
        result.Quality.Should().BeApproximately(0.9, 0.0001);
    }

    [Fact]
    public async Task Handle_SingleConfirmation_QualityEqualsGoodDividedByTotal()
    {
        // Arrange - 1 good + 2 scrap: quality = 1/3 rounded to 4 decimals
        var machineId = Guid.NewGuid();
        var from = Monday;
        var to = Monday.AddHours(8);
        ArrangeMachine(machineId);
        _confirmations.Setup(c => c.ListForMachineInWindowAsync(
                machineId, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Confirmation(machineId, Monday.AddHours(1), 1m, 2m)]);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeSummaryRequest(machineId, from, to), CancellationToken.None);

        // Assert
        result.TotalCount.Should().Be(3m);
        result.Quality.Should().BeApproximately(1.0 / 3.0, 0.0001);
        result.Quality.Should().Be(0.3333);
    }

    [Fact]
    public async Task Handle_EmptyPeriod_ReturnsZerosAndNullQuality()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        var from = Monday;
        var to = Monday.AddHours(8);
        ArrangeMachine(machineId);
        _confirmations.Setup(c => c.ListForMachineInWindowAsync(
                machineId, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeSummaryRequest(machineId, from, to), CancellationToken.None);

        // Assert
        result.GoodCount.Should().Be(0m);
        result.ScrapCount.Should().Be(0m);
        result.TotalCount.Should().Be(0m);
        result.Quality.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ForwardsNormalizedWindowToRepository()
    {
        // Arrange - local-kind inputs are normalized to UTC for the query
        var machineId = Guid.NewGuid();
        ArrangeMachine(machineId);
        _confirmations.Setup(c => c.ListForMachineInWindowAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeSummaryRequest(machineId, Monday, Monday.AddHours(8)), CancellationToken.None);

        // Assert
        result.FromUtc.Kind.Should().Be(DateTimeKind.Utc);
        result.ToUtc.Kind.Should().Be(DateTimeKind.Utc);
        _confirmations.Verify(c => c.ListForMachineInWindowAsync(
            machineId, Monday.ToUniversalTime(), Monday.AddHours(8).ToUniversalTime(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyMachineId_ThrowsValidationException()
    {
        // Act
        var act = () => CreateSut().Handle(
            new GetOeeSummaryRequest(Guid.Empty, Monday, Monday.AddHours(8)),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _machines.Verify(
            m => m.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MissingDates_ThrowsValidationException()
    {
        // Act
        var missingFrom = () => CreateSut().Handle(
            new GetOeeSummaryRequest(Guid.NewGuid(), default, Monday.AddHours(8)),
            CancellationToken.None);
        var missingTo = () => CreateSut().Handle(
            new GetOeeSummaryRequest(Guid.NewGuid(), Monday, default),
            CancellationToken.None);

        // Assert
        await missingFrom.Should().ThrowAsync<ValidationException>();
        await missingTo.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_ReversedWindow_ThrowsValidationException()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        ArrangeMachine(machineId);

        // Act
        var act = () => CreateSut().Handle(
            new GetOeeSummaryRequest(machineId, Monday.AddHours(8), Monday),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_EqualBoundsWindow_ThrowsValidationException()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        ArrangeMachine(machineId);

        // Act
        var act = () => CreateSut().Handle(
            new GetOeeSummaryRequest(machineId, Monday, Monday),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_UnknownMachine_ThrowsNotFoundException_WithoutTouchingData()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        _machines.Setup(m => m.GetByIdAsync(machineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Machine?)null);

        // Act
        var act = () => CreateSut().Handle(
            new GetOeeSummaryRequest(machineId, Monday, Monday.AddHours(8)),
            CancellationToken.None);

        // Assert - cross-tenant machines are invisible through the tenant
        // filter, so they surface here as unknown ids (404, never data)
        await act.Should().ThrowAsync<NotFoundException>();
        _confirmations.Verify(
            c => c.ListForMachineInWindowAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _calendars.Verify(
            c => c.GetByMachineIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _downtimes.Verify(
            d => d.ListOverlappingAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_CalendarAndClosedDowntime_ReturnsPlannedRunAndAvailability()
    {
        // Arrange - Monday 06:00-14:00 shift (480 planned minutes), one closed
        // 60-minute stop: run = 420, availability = 420/480 = 0.875
        var machineId = Guid.NewGuid();
        var from = Monday;
        var to = Monday.AddHours(8);
        ArrangeMachine(machineId);
        ArrangeCalendar(WorkingEntry(DayOfWeek.Monday, "06:00", "14:00"));
        ArrangeDowntimes(ClosedDowntime(machineId, Monday.AddHours(1), Monday.AddHours(2)));
        _confirmations.Setup(c => c.ListForMachineInWindowAsync(
                machineId, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeSummaryRequest(machineId, from, to), CancellationToken.None);

        // Assert
        result.PlannedTimeMinutes.Should().BeApproximately(480, 0.001);
        result.DowntimeMinutes.Should().BeApproximately(60, 0.001);
        result.RunTimeMinutes.Should().BeApproximately(420, 0.001);
        result.Availability.Should().BeApproximately(420.0 / 480.0, 0.0001);
    }

    [Fact]
    public async Task Handle_OpenDowntime_IsIgnored()
    {
        // Arrange - one closed 60-minute stop plus one open event: only the
        // closed overlap counts toward downtime
        var machineId = Guid.NewGuid();
        var from = Monday;
        var to = Monday.AddHours(8);
        ArrangeMachine(machineId);
        ArrangeCalendar(WorkingEntry(DayOfWeek.Monday, "06:00", "14:00"));
        ArrangeDowntimes(
            ClosedDowntime(machineId, Monday.AddHours(1), Monday.AddHours(2)),
            OpenDowntime(machineId, Monday.AddHours(3)));
        _confirmations.Setup(c => c.ListForMachineInWindowAsync(
                machineId, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeSummaryRequest(machineId, from, to), CancellationToken.None);

        // Assert
        result.DowntimeMinutes.Should().BeApproximately(60, 0.001);
        result.RunTimeMinutes.Should().BeApproximately(420, 0.001);
        result.Availability.Should().BeApproximately(420.0 / 480.0, 0.0001);
    }

    [Fact]
    public async Task Handle_NoCalendar_ReturnsZeroPlannedAndNullAvailability()
    {
        // Arrange - no calendar rows: planned time is zero, so Availability
        // is null even though downtime overlap still sums
        var machineId = Guid.NewGuid();
        var from = Monday;
        var to = Monday.AddHours(8);
        ArrangeMachine(machineId);
        ArrangeDowntimes(ClosedDowntime(machineId, Monday.AddHours(1), Monday.AddHours(2)));
        _confirmations.Setup(c => c.ListForMachineInWindowAsync(
                machineId, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeSummaryRequest(machineId, from, to), CancellationToken.None);

        // Assert
        result.PlannedTimeMinutes.Should().Be(0);
        result.DowntimeMinutes.Should().BeApproximately(60, 0.001);
        result.RunTimeMinutes.Should().Be(0);
        result.Availability.Should().BeNull();
    }

    [Fact]
    public async Task Handle_DowntimeExceedingPlanned_FloorsRunAtZeroWithZeroAvailability()
    {
        // Arrange - 60 planned minutes against a 300-minute stop overlapping
        // the window: run floors at zero and availability is 0 (not null,
        // never negative)
        var machineId = Guid.NewGuid();
        var from = Monday;
        var to = Monday.AddHours(8);
        ArrangeMachine(machineId);
        ArrangeCalendar(WorkingEntry(DayOfWeek.Monday, "06:00", "07:00"));
        ArrangeDowntimes(ClosedDowntime(machineId, Monday.AddHours(-2), Monday.AddHours(3)));
        _confirmations.Setup(c => c.ListForMachineInWindowAsync(
                machineId, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeSummaryRequest(machineId, from, to), CancellationToken.None);

        // Assert
        result.PlannedTimeMinutes.Should().BeApproximately(60, 0.001);
        result.DowntimeMinutes.Should().BeApproximately(180, 0.001);
        result.RunTimeMinutes.Should().Be(0);
        result.Availability.Should().Be(0);
    }

    [Fact]
    public async Task Handle_PartiallyOverlappingDowntime_ClipsToWindow()
    {
        // Arrange - stop starts two hours before the window and ends one hour
        // inside: only the 60-minute overlap counts
        var machineId = Guid.NewGuid();
        var from = Monday;
        var to = Monday.AddHours(8);
        ArrangeMachine(machineId);
        ArrangeCalendar(WorkingEntry(DayOfWeek.Monday, "06:00", "14:00"));
        ArrangeDowntimes(ClosedDowntime(machineId, Monday.AddHours(-2), Monday.AddHours(1)));
        _confirmations.Setup(c => c.ListForMachineInWindowAsync(
                machineId, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeSummaryRequest(machineId, from, to), CancellationToken.None);

        // Assert
        result.DowntimeMinutes.Should().BeApproximately(60, 0.001);
        result.RunTimeMinutes.Should().BeApproximately(420, 0.001);
        result.Availability.Should().BeApproximately(420.0 / 480.0, 0.0001);
    }

    [Fact]
    public async Task Handle_ForwardsNormalizedWindowToAvailabilityRepositories()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        ArrangeMachine(machineId);
        _confirmations.Setup(c => c.ListForMachineInWindowAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await CreateSut().Handle(
            new GetOeeSummaryRequest(machineId, Monday, Monday.AddHours(8)), CancellationToken.None);

        // Assert - calendar and downtime queries use the same normalized window
        _calendars.Verify(c => c.GetByMachineIdAsync(machineId, It.IsAny<CancellationToken>()), Times.Once);
        _downtimes.Verify(d => d.ListOverlappingAsync(
            machineId, Monday.ToUniversalTime(), Monday.AddHours(8).ToUniversalTime(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
