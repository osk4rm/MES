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

    private static WorkCenterCalendarEntry FullDayEntry(DayOfWeek day) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        WorkCenterCalendarId = Guid.NewGuid(),
        DayOfWeek = day,
        StartTime = TimeOnly.Parse("00:00"),
        EndTime = TimeOnly.Parse("00:00"),
        IsWorking = true
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
    public async Task Handle_DayWindow_SplitsAtMidnightsAscendingAndContiguous()
    {
        // Arrange - Monday 06:00 to Wednesday 06:00 spans three calendar days
        var machineId = Guid.NewGuid();
        var from = Monday.AddHours(6);
        var to = Monday.AddDays(2).AddHours(6);
        ArrangeMachineWithCalendar(
            machineId,
            FullDayEntry(DayOfWeek.Monday),
            FullDayEntry(DayOfWeek.Tuesday),
            FullDayEntry(DayOfWeek.Wednesday));

        // Act
        var result = await CreateSut().Handle(
            new GetOeeTrendRequest(machineId, from, to, 60m, "Day"), CancellationToken.None);

        // Assert
        result.MachineId.Should().Be(machineId);
        result.FromUtc.Should().Be(from);
        result.ToUtc.Should().Be(to);
        result.IdealCycleTimeSeconds.Should().Be(60m);
        result.Bucket.Should().Be("Day");
        result.Buckets.Should().HaveCount(3);
        result.Buckets[0].FromUtc.Should().Be(from);
        result.Buckets[0].ToUtc.Should().Be(Monday.AddDays(1));
        result.Buckets[1].FromUtc.Should().Be(Monday.AddDays(1));
        result.Buckets[1].ToUtc.Should().Be(Monday.AddDays(2));
        result.Buckets[2].FromUtc.Should().Be(Monday.AddDays(2));
        result.Buckets[2].ToUtc.Should().Be(to);
        result.Buckets.Select(b => b.FromUtc).Should().BeInAscendingOrder();
        result.Buckets.Should().OnlyContain(b => b.MachineId == machineId);
        result.Buckets.Should().OnlyContain(b => b.IdealCycleTimeSeconds == 60m);
    }

    [Fact]
    public async Task Handle_ExactDayWindow_ReturnsOneBucketPerDay()
    {
        // Arrange - Monday 00:00 to Wednesday 00:00 is exactly two days
        var machineId = Guid.NewGuid();
        ArrangeMachineWithCalendar(
            machineId,
            FullDayEntry(DayOfWeek.Monday),
            FullDayEntry(DayOfWeek.Tuesday));

        // Act
        var result = await CreateSut().Handle(
            new GetOeeTrendRequest(machineId, Monday, Monday.AddDays(2), 60m, "Day"),
            CancellationToken.None);

        // Assert
        result.Buckets.Should().HaveCount(2);
        result.Buckets[0].FromUtc.Should().Be(Monday);
        result.Buckets[0].ToUtc.Should().Be(Monday.AddDays(1));
        result.Buckets[1].FromUtc.Should().Be(Monday.AddDays(1));
        result.Buckets[1].ToUtc.Should().Be(Monday.AddDays(2));

        // Each full day carries 1440 planned minutes with no stops or output:
        // availability computes to 1.0 while performance and quality stay null.
        result.Buckets.Should().OnlyContain(b =>
            b.PlannedProductionTimeMinutes == 1440
            && b.Availability == 1.0
            && b.AvailabilityComputed
            && b.Performance == null
            && b.Quality == null
            && b.Oee == null);
    }

    [Fact]
    public async Task Handle_WeekWindow_SplitsAtMondayMidnight()
    {
        // Arrange - Wednesday 12:00 to next Wednesday 12:00 crosses one Monday
        var machineId = Guid.NewGuid();
        var from = Monday.AddDays(2).AddHours(12);
        var to = from.AddDays(7);
        ArrangeMachineWithCalendar(
            machineId,
            FullDayEntry(DayOfWeek.Monday),
            FullDayEntry(DayOfWeek.Tuesday),
            FullDayEntry(DayOfWeek.Wednesday),
            FullDayEntry(DayOfWeek.Thursday),
            FullDayEntry(DayOfWeek.Friday),
            FullDayEntry(DayOfWeek.Saturday),
            FullDayEntry(DayOfWeek.Sunday));

        // Act
        var result = await CreateSut().Handle(
            new GetOeeTrendRequest(machineId, from, to, 60m, "Week"), CancellationToken.None);

        // Assert
        result.Bucket.Should().Be("Week");
        result.Buckets.Should().HaveCount(2);
        result.Buckets[0].FromUtc.Should().Be(from);
        result.Buckets[0].ToUtc.Should().Be(Monday.AddDays(7));
        result.Buckets[1].FromUtc.Should().Be(Monday.AddDays(7));
        result.Buckets[1].ToUtc.Should().Be(to);
    }

    [Fact]
    public async Task Handle_BucketNames_AreCaseInsensitiveAndNormalized()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        ArrangeMachineWithCalendar(machineId, FullDayEntry(DayOfWeek.Monday));

        // Act
        var lower = await CreateSut().Handle(
            new GetOeeTrendRequest(machineId, Monday, Monday.AddHours(8), 60m, "day"),
            CancellationToken.None);
        var upper = await CreateSut().Handle(
            new GetOeeTrendRequest(machineId, Monday, Monday.AddHours(8), 60m, "WEEK"),
            CancellationToken.None);

        // Assert
        lower.Bucket.Should().Be("Day");
        lower.Buckets.Should().HaveCount(1);
        upper.Bucket.Should().Be("Week");
        upper.Buckets.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_EmptyBuckets_CarryNullFactorsNotZeros()
    {
        // Arrange - no calendar, so no bucket has planned time
        var machineId = Guid.NewGuid();
        _machines.Setup(m => m.GetByIdAsync(machineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AMachine(machineId));
        _calendars.Setup(c => c.GetByMachineIdAsync(machineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkCenterCalendar?)null);
        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _confirmations.Setup(c => c.ListForMachineInWindowAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Confirmation(machineId, Monday.AddHours(10), 10m, 0m)]);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeTrendRequest(machineId, Monday, Monday.AddDays(2), 60m, "Day"),
            CancellationToken.None);

        // Assert - both buckets exist and report nulls even though output exists
        result.Buckets.Should().HaveCount(2);
        result.Buckets.Should().OnlyContain(b =>
            b.Availability == null
            && b.Performance == null
            && b.Quality == null
            && b.Oee == null
            && !b.AvailabilityComputed
            && !b.PerformanceComputed
            && !b.QualityComputed);
    }

    [Fact]
    public async Task Handle_BucketEntry_MatchesSnapshotMathForThatBucket()
    {
        // Arrange - Tuesday 00:00-24:00 with a 1h stop and 90 good + 10 scrap:
        // availability = 1380/1440 = 0.9583, performance = 100/1380 = 0.0725,
        // quality = 0.9, oee = 0.9583*0.0725*0.9 = 0.0625
        var machineId = Guid.NewGuid();
        ArrangeMachineWithCalendar(
            machineId,
            FullDayEntry(DayOfWeek.Monday),
            FullDayEntry(DayOfWeek.Tuesday));
        var tuesday = Monday.AddDays(1);

        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([ClosedDowntime(machineId, tuesday.AddHours(8), tuesday.AddHours(9))]);
        _confirmations.Setup(c => c.ListForMachineInWindowAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Confirmation(machineId, tuesday.AddHours(10), 90m, 10m)]);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeTrendRequest(machineId, Monday, Monday.AddDays(2), 60m, "Day"),
            CancellationToken.None);

        // Assert - Monday bucket is empty, Tuesday bucket matches the snapshot
        result.Buckets.Should().HaveCount(2);
        result.Buckets[0].Availability.Should().Be(1.0);
        result.Buckets[0].TotalCount.Should().Be(0m);
        var entry = result.Buckets[1];
        entry.FromUtc.Should().Be(tuesday);
        entry.ToUtc.Should().Be(tuesday.AddDays(1));
        entry.PlannedProductionTimeMinutes.Should().Be(1440);
        entry.DowntimeMinutes.Should().Be(60);
        entry.RunTimeMinutes.Should().Be(1380);
        entry.TotalCount.Should().Be(100m);
        entry.Availability.Should().Be(0.9583);
        entry.Performance.Should().Be(0.0725);
        entry.Quality.Should().Be(0.9);
        entry.Oee.Should().Be(0.0625);
    }

    [Fact]
    public async Task Handle_ValuesSplitAcrossBuckets_SumToWindowTotals()
    {
        // Arrange - a stop and a confirmation each straddle the midnight boundary
        var machineId = Guid.NewGuid();
        ArrangeMachineWithCalendar(
            machineId,
            FullDayEntry(DayOfWeek.Monday),
            FullDayEntry(DayOfWeek.Tuesday));
        var tuesday = Monday.AddDays(1);

        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([ClosedDowntime(
                machineId, Monday.AddHours(23), tuesday.AddHours(1))]);
        _confirmations.Setup(c => c.ListForMachineInWindowAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Confirmation(machineId, Monday.AddHours(22), 10m, 0m),
                Confirmation(machineId, tuesday, 20m, 5m)]);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeTrendRequest(machineId, Monday, Monday.AddDays(2), 60m, "Day"),
            CancellationToken.None);

        // Assert - the stop contributes 60 minutes to each bucket and the
        // midnight-stamped confirmation belongs to Tuesday only (counted once)
        result.Buckets[0].DowntimeMinutes.Should().Be(60);
        result.Buckets[1].DowntimeMinutes.Should().Be(60);
        result.Buckets.Select(b => b.DowntimeMinutes).Sum().Should().Be(120);
        result.Buckets[0].TotalCount.Should().Be(10m);
        result.Buckets[1].TotalCount.Should().Be(25m);
        result.Buckets.Select(b => (double)b.TotalCount).Sum().Should().Be(35d);
    }

    [Theory]
    [InlineData("Month")]
    [InlineData("daily")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public async Task Handle_UnknownBucket_ThrowsValidationException(string? bucket)
    {
        // Arrange
        var machineId = Guid.NewGuid();
        ArrangeMachineWithCalendar(machineId, FullDayEntry(DayOfWeek.Monday));

        // Act
        var act = () => CreateSut().Handle(
            new GetOeeTrendRequest(machineId, Monday, Monday.AddHours(8), 60m, bucket),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_InvalidNumericBucket_ThrowsValidationException()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        ArrangeMachineWithCalendar(machineId, FullDayEntry(DayOfWeek.Monday));

        // Act
        var act = () => CreateSut().Handle(
            new GetOeeTrendRequest(machineId, Monday, Monday.AddHours(8), 60m, "99"),
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
            new GetOeeTrendRequest(machineId, Monday.AddHours(8), Monday, 60m, "Day"),
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
            new GetOeeTrendRequest(machineId, Monday, Monday.AddHours(8), ideal, "Day"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_EmptyMachineId_ThrowsValidationException()
    {
        // Act
        var act = () => CreateSut().Handle(
            new GetOeeTrendRequest(Guid.Empty, Monday, Monday.AddHours(8), 60m, "Day"),
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
            new GetOeeTrendRequest(machineId, Monday, Monday.AddHours(8), 60m, "Day"),
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
        _confirmations.Verify(
            c => c.ListForMachineInWindowAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
