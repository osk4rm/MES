using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Oee.Trend;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class GetOeeTrendRequestHandlerTests
{
    private static readonly DateTime Monday = new(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IMachinesRepository> _machines = new();
    private readonly Mock<IWorkCenterCalendarsRepository> _calendars = new();
    private readonly Mock<IDowntimeEventsRepository> _downtimes = new();
    private readonly Mock<IProductionConfirmationsRepository> _confirmations = new();

    private GetOeeTrendRequestHandler CreateSut() => new(
        _machines.Object, _calendars.Object, _downtimes.Object, _confirmations.Object);

    private static Machine AMachine(Guid id) => new()
    {
        Id = id,
        TenantId = Guid.NewGuid(),
        Code = "WC-1",
        Name = "Work Center 1"
    };

    private static WorkCenterCalendarEntry Entry(
        DayOfWeek day, string start, string end, bool isWorking = true) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        WorkCenterCalendarId = Guid.NewGuid(),
        DayOfWeek = day,
        StartTime = TimeOnly.Parse(start),
        EndTime = TimeOnly.Parse(end),
        IsWorking = isWorking
    };

    private static DowntimeEvent ClosedDowntime(Guid machineId, DateTime start, DateTime end) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        MachineId = machineId,
        ReasonCodeId = Guid.NewGuid(),
        StartedAt = start,
        EndedAt = end
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

    private void ArrangeMachineWithCalendar(Guid machineId, params WorkCenterCalendarEntry[] entries)
    {
        _machines.Setup(m => m.GetByIdAsync(machineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AMachine(machineId));
        _calendars.Setup(c => c.GetByMachineIdAsync(machineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkCenterCalendar
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                MachineId = machineId,
                Entries = entries.ToList()
            });
        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _confirmations.Setup(c => c.ListForMachineInWindowAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    [Fact]
    public void Request_ImplementsTenantRequest_AndNotAnonymous()
    {
        // Assert - the trend query must stay tenant-scoped
        typeof(ITenantRequest<OeeTrendResponse>)
            .IsAssignableFrom(typeof(GetOeeTrendRequest)).Should().BeTrue();
        typeof(IAllowAnonymousRequest).IsAssignableFrom(typeof(GetOeeTrendRequest)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DayBuckets_PartitionAtMidnight_Ascending()
    {
        // Arrange - Mon 06:00 -> Wed 06:00 with full-day entries: buckets are
        // [Mon06:00, Tue00:00) = 1080min, [Tue00:00, Wed00:00) = 1440min,
        // [Wed00:00, Wed06:00] = 360min
        var machineId = Guid.NewGuid();
        ArrangeMachineWithCalendar(
            machineId,
            Entry(DayOfWeek.Monday, "00:00", "00:00"),
            Entry(DayOfWeek.Tuesday, "00:00", "00:00"),
            Entry(DayOfWeek.Wednesday, "00:00", "00:00"));

        // Act
        var result = await CreateSut().Handle(
            new GetOeeTrendRequest(machineId, Monday.AddHours(6), Monday.AddDays(2).AddHours(6), 60m, "Day"),
            CancellationToken.None);

        // Assert
        result.MachineId.Should().Be(machineId);
        result.Bucket.Should().Be("Day");
        result.Entries.Should().HaveCount(3);
        result.Entries[0].FromUtc.Should().Be(Monday.AddHours(6));
        result.Entries[0].ToUtc.Should().Be(Monday.AddDays(1));
        result.Entries[1].FromUtc.Should().Be(Monday.AddDays(1));
        result.Entries[1].ToUtc.Should().Be(Monday.AddDays(2));
        result.Entries[2].FromUtc.Should().Be(Monday.AddDays(2));
        result.Entries[2].ToUtc.Should().Be(Monday.AddDays(2).AddHours(6));
        result.Entries[0].PlannedProductionTimeMinutes.Should().Be(1080);
        result.Entries[1].PlannedProductionTimeMinutes.Should().Be(1440);
        result.Entries[2].PlannedProductionTimeMinutes.Should().Be(360);
    }

    [Fact]
    public async Task Handle_WeekBuckets_SplitAtMondayMidnight()
    {
        // Arrange - Mon 06:00 -> next Mon 06:00: buckets are
        // [Mon06:00, nextMon00:00) and [nextMon00:00, nextMon06:00]
        var machineId = Guid.NewGuid();
        ArrangeMachineWithCalendar(
            machineId,
            Entry(DayOfWeek.Monday, "00:00", "00:00"),
            Entry(DayOfWeek.Tuesday, "00:00", "00:00"),
            Entry(DayOfWeek.Wednesday, "00:00", "00:00"),
            Entry(DayOfWeek.Thursday, "00:00", "00:00"),
            Entry(DayOfWeek.Friday, "00:00", "00:00"),
            Entry(DayOfWeek.Saturday, "00:00", "00:00"),
            Entry(DayOfWeek.Sunday, "00:00", "00:00"));

        // Act
        var result = await CreateSut().Handle(
            new GetOeeTrendRequest(machineId, Monday.AddHours(6), Monday.AddDays(7).AddHours(6), 60m, "Week"),
            CancellationToken.None);

        // Assert
        result.Bucket.Should().Be("Week");
        result.Entries.Should().HaveCount(2);
        result.Entries[0].FromUtc.Should().Be(Monday.AddHours(6));
        result.Entries[0].ToUtc.Should().Be(Monday.AddDays(7));
        result.Entries[1].FromUtc.Should().Be(Monday.AddDays(7));
        result.Entries[1].ToUtc.Should().Be(Monday.AddDays(7).AddHours(6));
        result.Entries[0].PlannedProductionTimeMinutes.Should().Be(9720);
        result.Entries[1].PlannedProductionTimeMinutes.Should().Be(360);
    }

    [Fact]
    public async Task Handle_SingleDayBucket_MatchesSnapshotMath()
    {
        // Arrange - same numbers as the (1/3) snapshot happy path:
        // availability = 420/480 = 0.875, performance = 100/420 = 0.2381,
        // quality = 0.9, oee = 0.1875
        var machineId = Guid.NewGuid();
        var from = Monday.AddHours(6);
        var to = Monday.AddHours(14);
        ArrangeMachineWithCalendar(machineId, Entry(Monday.DayOfWeek, "06:00", "14:00"));

        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ClosedDowntime(machineId, Monday.AddHours(8), Monday.AddHours(9))]);
        _confirmations.Setup(c => c.ListForMachineInWindowAsync(
                machineId, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Confirmation(machineId, Monday.AddHours(10), 50m, 5m),
                Confirmation(machineId, Monday.AddHours(11), 40m, 5m)]);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeTrendRequest(machineId, from, to, 60m, "Day"),
            CancellationToken.None);

        // Assert
        result.Entries.Should().ContainSingle();
        var entry = result.Entries[0];
        entry.FromUtc.Should().Be(from);
        entry.ToUtc.Should().Be(to);
        entry.Availability.Should().Be(0.875);
        entry.Performance.Should().Be(0.2381);
        entry.Quality.Should().Be(0.9);
        entry.Oee.Should().Be(0.1875);
    }

    [Fact]
    public async Task Handle_DowntimeSlicedPerBucket_SumsToWindowTotal()
    {
        // Arrange - a 90min stop spanning the Mon/Tue midnight boundary:
        // 60min fall in bucket 1, 30min in bucket 2
        var machineId = Guid.NewGuid();
        ArrangeMachineWithCalendar(
            machineId,
            Entry(DayOfWeek.Monday, "00:00", "00:00"),
            Entry(DayOfWeek.Tuesday, "00:00", "00:00"));

        var from = Monday.AddHours(12);
        var to = Monday.AddDays(1).AddHours(12);
        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ClosedDowntime(
                machineId, Monday.AddHours(23), Monday.AddDays(1).AddMinutes(30))]);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeTrendRequest(machineId, from, to, 60m, "Day"),
            CancellationToken.None);

        // Assert
        result.Entries.Should().HaveCount(2);
        result.Entries[0].DowntimeMinutes.Should().Be(60);
        result.Entries[1].DowntimeMinutes.Should().Be(30);
        result.Entries.Sum(e => e.DowntimeMinutes).Should().Be(90);
    }

    [Fact]
    public async Task Handle_EmptyBucket_CarriesNullFactorsNotZeros()
    {
        // Arrange - calendar covers Monday only; the Tuesday bucket is empty
        var machineId = Guid.NewGuid();
        ArrangeMachineWithCalendar(machineId, Entry(DayOfWeek.Monday, "06:00", "14:00"));

        // Act
        var result = await CreateSut().Handle(
            new GetOeeTrendRequest(machineId, Monday.AddHours(6), Monday.AddDays(1).AddHours(14), 60m, "Day"),
            CancellationToken.None);

        // Assert
        result.Entries.Should().HaveCount(2);
        result.Entries[0].PlannedProductionTimeMinutes.Should().Be(480);
        result.Entries[0].Availability.Should().Be(1.0);
        result.Entries[1].PlannedProductionTimeMinutes.Should().Be(0);
        result.Entries[1].Availability.Should().BeNull();
        result.Entries[1].Performance.Should().BeNull();
        result.Entries[1].Quality.Should().BeNull();
        result.Entries[1].Oee.Should().BeNull();
        result.Entries[1].AvailabilityComputed.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_BucketIsCaseInsensitive()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        ArrangeMachineWithCalendar(machineId);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeTrendRequest(machineId, Monday.AddHours(6), Monday.AddHours(14), 60m, "day"),
            CancellationToken.None);

        // Assert
        result.Bucket.Should().Be("Day");
        result.Entries.Should().ContainSingle();
    }

    [Theory]
    [InlineData("Month")]
    [InlineData("")]
    [InlineData("daily")]
    public async Task Handle_UnknownBucket_ThrowsValidationException(string bucket)
    {
        // Arrange
        var machineId = Guid.NewGuid();
        ArrangeMachineWithCalendar(machineId);

        // Act
        var act = () => CreateSut().Handle(
            new GetOeeTrendRequest(machineId, Monday.AddHours(6), Monday.AddHours(14), 60m, bucket),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_ReversedWindow_ThrowsValidationException()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        ArrangeMachineWithCalendar(machineId);

        // Act
        var act = () => CreateSut().Handle(
            new GetOeeTrendRequest(machineId, Monday.AddHours(14), Monday.AddHours(6), 60m, "Day"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_WindowOver93Days_ThrowsValidationException()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        ArrangeMachineWithCalendar(machineId);

        // Act
        var act = () => CreateSut().Handle(
            new GetOeeTrendRequest(machineId, Monday, Monday.AddDays(93).AddMinutes(1), 60m, "Day"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-60)]
    public async Task Handle_NonPositiveIdealCycleTime_ThrowsValidationException(decimal ideal)
    {
        // Arrange
        var machineId = Guid.NewGuid();
        ArrangeMachineWithCalendar(machineId);

        // Act
        var act = () => CreateSut().Handle(
            new GetOeeTrendRequest(machineId, Monday.AddHours(6), Monday.AddHours(14), ideal, "Day"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_EmptyMachineId_ThrowsValidationException()
    {
        // Act
        var act = () => CreateSut().Handle(
            new GetOeeTrendRequest(Guid.Empty, Monday.AddHours(6), Monday.AddHours(14), 60m, "Day"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _machines.Verify(
            m => m.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
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
            new GetOeeTrendRequest(machineId, Monday.AddHours(6), Monday.AddHours(14), 60m, "Day"),
            CancellationToken.None);

        // Assert - cross-tenant machines are invisible through the tenant
        // filter, so they surface here as unknown ids (404, never data)
        await act.Should().ThrowAsync<NotFoundException>();
        _calendars.Verify(
            c => c.GetByMachineIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _downtimes.Verify(
            d => d.ListOverlappingAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
