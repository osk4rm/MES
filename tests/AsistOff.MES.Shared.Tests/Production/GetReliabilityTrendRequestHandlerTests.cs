using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Reliability.Trend;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class GetReliabilityTrendRequestHandlerTests
{
    private static readonly DateTime Monday = new(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IMachinesRepository> _machines = new();
    private readonly Mock<IDowntimeEventsRepository> _downtimes = new();
    private readonly Mock<IMaintenanceWorkOrdersRepository> _repairs = new();

    private GetReliabilityTrendRequestHandler CreateSut() => new(
        _machines.Object, _downtimes.Object, _repairs.Object);

    private static Machine AMachine(Guid id) => new()
    {
        Id = id,
        TenantId = Guid.NewGuid(),
        Code = "WC-1",
        Name = "Work Center 1"
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

    private static DowntimeEvent OpenDowntime(Guid machineId, DateTime start) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        MachineId = machineId,
        ReasonCodeId = Guid.NewGuid(),
        StartedAt = start,
        EndedAt = null
    };

    private static MaintenanceWorkOrder DoneRepair(
        Guid machineId, DateTime reportedAt, DateTime? startedAt, DateTime completedAt) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        Code = $"WO-{Guid.NewGuid():N}"[..12],
        Title = "Fix",
        MachineId = machineId,
        Priority = MaintenanceWorkOrderPriority.Medium,
        Status = MaintenanceWorkOrderStatus.Done,
        ReportedAt = reportedAt,
        StartedAt = startedAt,
        CompletedAt = completedAt
    };

    private static MaintenanceWorkOrder OpenRepair(Guid machineId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        Code = $"WO-{Guid.NewGuid():N}"[..12],
        Title = "Fix",
        MachineId = machineId,
        Priority = MaintenanceWorkOrderPriority.Medium,
        Status = MaintenanceWorkOrderStatus.Open,
        ReportedAt = Monday.AddHours(10),
        StartedAt = null,
        CompletedAt = null
    };

    private static MaintenanceWorkOrder InProgressRepair(Guid machineId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        Code = $"WO-{Guid.NewGuid():N}"[..12],
        Title = "Fix",
        MachineId = machineId,
        Priority = MaintenanceWorkOrderPriority.Medium,
        Status = MaintenanceWorkOrderStatus.InProgress,
        ReportedAt = Monday.AddHours(9),
        StartedAt = Monday.AddHours(10),
        CompletedAt = null
    };

    private void ArrangeMachine(Guid machineId)
    {
        _machines.Setup(m => m.GetByIdAsync(machineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AMachine(machineId));
        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _repairs.Setup(r => r.ListDoneInWindowAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    [Fact]
    public void Request_ImplementsTenantRequest_AndNotAnonymous()
    {
        // Assert - the trend query must stay tenant-scoped
        typeof(ITenantRequest<ReliabilityTrendResponse>)
            .IsAssignableFrom(typeof(GetReliabilityTrendRequest)).Should().BeTrue();
        typeof(IAllowAnonymousRequest).IsAssignableFrom(typeof(GetReliabilityTrendRequest)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DayWindow_SplitsAtMidnightsAscendingAndContiguous()
    {
        // Arrange - Monday 06:00 to Wednesday 06:00 spans three calendar days
        var machineId = Guid.NewGuid();
        var from = Monday.AddHours(6);
        var to = Monday.AddDays(2).AddHours(6);
        ArrangeMachine(machineId);

        // Act
        var result = await CreateSut().Handle(
            new GetReliabilityTrendRequest(machineId, from, to, "Day"), CancellationToken.None);

        // Assert
        result.MachineId.Should().Be(machineId);
        result.FromUtc.Should().Be(from);
        result.ToUtc.Should().Be(to);
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
    }

    [Fact]
    public async Task Handle_TwoWeekWindow_WeekBuckets_ReturnOneEntryPerBucket()
    {
        // Arrange - Monday 00:00 to Monday +14d is exactly two calendar weeks
        var machineId = Guid.NewGuid();
        ArrangeMachine(machineId);

        // Act
        var result = await CreateSut().Handle(
            new GetReliabilityTrendRequest(machineId, Monday, Monday.AddDays(14), "Week"),
            CancellationToken.None);

        // Assert
        result.Bucket.Should().Be("Week");
        result.Buckets.Should().HaveCount(2);
        result.Buckets[0].FromUtc.Should().Be(Monday);
        result.Buckets[0].ToUtc.Should().Be(Monday.AddDays(7));
        result.Buckets[1].FromUtc.Should().Be(Monday.AddDays(7));
        result.Buckets[1].ToUtc.Should().Be(Monday.AddDays(14));
    }

    [Fact]
    public async Task Handle_WeekWindowMidWeek_SplitsAtMondayMidnight()
    {
        // Arrange - Wednesday 12:00 to next Wednesday 12:00 crosses one Monday
        var machineId = Guid.NewGuid();
        var from = Monday.AddDays(2).AddHours(12);
        var to = from.AddDays(7);
        ArrangeMachine(machineId);

        // Act
        var result = await CreateSut().Handle(
            new GetReliabilityTrendRequest(machineId, from, to, "Week"), CancellationToken.None);

        // Assert
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
        ArrangeMachine(machineId);

        // Act
        var lower = await CreateSut().Handle(
            new GetReliabilityTrendRequest(machineId, Monday, Monday.AddHours(8), "day"),
            CancellationToken.None);
        var upper = await CreateSut().Handle(
            new GetReliabilityTrendRequest(machineId, Monday, Monday.AddHours(8), "WEEK"),
            CancellationToken.None);

        // Assert
        lower.Bucket.Should().Be("Day");
        lower.Buckets.Should().HaveCount(1);
        upper.Bucket.Should().Be("Week");
        upper.Buckets.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_BucketEntry_MatchesSnapshotMathForThatBucket()
    {
        // Arrange - two-day window, Tuesday holds a 60-min closed stop and one
        // 90-min Done repair: Monday bucket is empty, Tuesday matches the
        // snapshot (failure 1, downtime 60, uptime 1380, mtbf 1380, mttr 60).
        var machineId = Guid.NewGuid();
        ArrangeMachine(machineId);
        var tuesday = Monday.AddDays(1);

        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([ClosedDowntime(machineId, tuesday.AddHours(8), tuesday.AddHours(9))]);
        _repairs.Setup(r => r.ListDoneInWindowAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([DoneRepair(machineId, tuesday.AddHours(7), tuesday.AddHours(9), tuesday.AddHours(10).AddMinutes(30))]);

        // Act
        var result = await CreateSut().Handle(
            new GetReliabilityTrendRequest(machineId, Monday, Monday.AddDays(2), "Day"),
            CancellationToken.None);

        // Assert
        result.Buckets.Should().HaveCount(2);
        var mondayBucket = result.Buckets[0];
        mondayBucket.FailureCount.Should().Be(0);
        mondayBucket.RepairCount.Should().Be(0);
        mondayBucket.MtbfMinutes.Should().BeNull();
        mondayBucket.MttrMinutes.Should().BeNull();
        mondayBucket.AvgRepairMinutes.Should().BeNull();
        mondayBucket.WindowMinutes.Should().Be(1440);
        mondayBucket.UptimeMinutes.Should().Be(1440);

        var entry = result.Buckets[1];
        entry.FromUtc.Should().Be(tuesday);
        entry.ToUtc.Should().Be(tuesday.AddDays(1));
        entry.FailureCount.Should().Be(1);
        entry.RepairCount.Should().Be(1);
        entry.WindowMinutes.Should().Be(1440);
        entry.TotalDowntimeMinutes.Should().Be(60);
        entry.UptimeMinutes.Should().Be(1380);
        entry.MtbfMinutes.Should().Be(1380);
        entry.MttrMinutes.Should().Be(60);
        entry.AvgRepairMinutes.Should().Be(90);
    }

    [Fact]
    public async Task Handle_ZeroFailures_ReturnsNullMtbfMttr_NotZeros()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        ArrangeMachine(machineId);

        // Act
        var result = await CreateSut().Handle(
            new GetReliabilityTrendRequest(machineId, Monday, Monday.AddDays(2), "Day"),
            CancellationToken.None);

        // Assert - every bucket carries nulls, never zeros
        result.Buckets.Should().HaveCount(2);
        result.Buckets.Should().OnlyContain(b =>
            b.FailureCount == 0
            && b.MtbfMinutes == null
            && b.MttrMinutes == null
            && b.RepairCount == 0
            && b.AvgRepairMinutes == null);
    }

    [Fact]
    public async Task Handle_OpenDowntime_IgnoredInEveryBucket()
    {
        // Arrange - a still-open stop must not change failure counts
        var machineId = Guid.NewGuid();
        ArrangeMachine(machineId);

        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([OpenDowntime(machineId, Monday.AddHours(7))]);

        // Act
        var result = await CreateSut().Handle(
            new GetReliabilityTrendRequest(machineId, Monday, Monday.AddDays(2), "Day"),
            CancellationToken.None);

        // Assert
        result.Buckets.Should().HaveCount(2);
        result.Buckets.Should().OnlyContain(b =>
            b.FailureCount == 0
            && b.TotalDowntimeMinutes == 0
            && b.MtbfMinutes == null
            && b.MttrMinutes == null);
    }

    [Fact]
    public async Task Handle_NonDoneRepairs_Ignored()
    {
        // Arrange - Open and InProgress rows never count toward repairs
        var machineId = Guid.NewGuid();
        ArrangeMachine(machineId);

        _repairs.Setup(r => r.ListDoneInWindowAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([OpenRepair(machineId), InProgressRepair(machineId)]);

        // Act
        var result = await CreateSut().Handle(
            new GetReliabilityTrendRequest(machineId, Monday, Monday.AddHours(8), "Day"),
            CancellationToken.None);

        // Assert - the handler defensively filters non-Done rows
        result.Buckets.Should().HaveCount(1);
        result.Buckets[0].RepairCount.Should().Be(0);
        result.Buckets[0].AvgRepairMinutes.Should().BeNull();
    }

    [Fact]
    public async Task Handle_DoneOnlyRepairFiltering_CompletedAtBucketing()
    {
        // Arrange - two Done repairs completed on different days land in
        // different buckets; a repair completed exactly at midnight belongs
        // to the later bucket only (counted once).
        var machineId = Guid.NewGuid();
        ArrangeMachine(machineId);
        var tuesday = Monday.AddDays(1);

        _repairs.Setup(r => r.ListDoneInWindowAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                DoneRepair(machineId, Monday.AddHours(7), Monday.AddHours(8), Monday.AddHours(9)),
                DoneRepair(machineId, tuesday.AddHours(7), tuesday.AddHours(8), tuesday.AddHours(9)),
                DoneRepair(machineId, Monday.AddHours(22), Monday.AddHours(23), tuesday)]);

        // Act
        var result = await CreateSut().Handle(
            new GetReliabilityTrendRequest(machineId, Monday, Monday.AddDays(2), "Day"),
            CancellationToken.None);

        // Assert
        result.Buckets.Should().HaveCount(2);
        result.Buckets[0].RepairCount.Should().Be(1);
        result.Buckets[1].RepairCount.Should().Be(2);
        result.Buckets.Select(b => b.RepairCount).Sum().Should().Be(3);
        result.Buckets[0].AvgRepairMinutes.Should().Be(60);
        result.Buckets[1].AvgRepairMinutes.Should().Be(60);
    }

    [Fact]
    public async Task Handle_PartialDowntimeOverlap_CountsOverlapOnlyPerBucket()
    {
        // Arrange - a 2h stop straddling midnight contributes 60 min to each
        // bucket and counts as one failure in each bucket.
        var machineId = Guid.NewGuid();
        ArrangeMachine(machineId);
        var tuesday = Monday.AddDays(1);

        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([ClosedDowntime(machineId, Monday.AddHours(23), tuesday.AddHours(1))]);

        // Act
        var result = await CreateSut().Handle(
            new GetReliabilityTrendRequest(machineId, Monday, Monday.AddDays(2), "Day"),
            CancellationToken.None);

        // Assert
        result.Buckets.Should().HaveCount(2);
        result.Buckets[0].FailureCount.Should().Be(1);
        result.Buckets[0].TotalDowntimeMinutes.Should().Be(60);
        result.Buckets[0].UptimeMinutes.Should().Be(1380);
        result.Buckets[0].MtbfMinutes.Should().Be(1380);
        result.Buckets[0].MttrMinutes.Should().Be(60);
        result.Buckets[1].FailureCount.Should().Be(1);
        result.Buckets[1].TotalDowntimeMinutes.Should().Be(60);
        result.Buckets.Select(b => b.TotalDowntimeMinutes).Sum().Should().Be(120);
    }

    [Fact]
    public async Task Handle_FetchesFullWindowOnce_SlicesInMemory()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        var from = Monday;
        var to = Monday.AddDays(2);
        ArrangeMachine(machineId);

        // Act
        await CreateSut().Handle(
            new GetReliabilityTrendRequest(machineId, from, to, "Day"), CancellationToken.None);

        // Assert - one full-window fetch per source, never per bucket
        _downtimes.Verify(d => d.ListOverlappingAsync(
            machineId, from, to, It.IsAny<CancellationToken>()), Times.Once);
        _repairs.Verify(r => r.ListDoneInWindowAsync(
            machineId, from, to, It.IsAny<CancellationToken>()), Times.Once);
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
        ArrangeMachine(machineId);

        // Act
        var act = () => CreateSut().Handle(
            new GetReliabilityTrendRequest(machineId, Monday, Monday.AddHours(8), bucket),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_InvalidNumericBucket_ThrowsValidationException()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        ArrangeMachine(machineId);

        // Act
        var act = () => CreateSut().Handle(
            new GetReliabilityTrendRequest(machineId, Monday, Monday.AddHours(8), "99"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_ReversedWindow_ThrowsValidationException()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        ArrangeMachine(machineId);

        // Act
        var act = () => CreateSut().Handle(
            new GetReliabilityTrendRequest(machineId, Monday.AddHours(8), Monday, "Day"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_WindowOver93Days_ThrowsValidationException()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        ArrangeMachine(machineId);

        // Act
        var act = () => CreateSut().Handle(
            new GetReliabilityTrendRequest(machineId, Monday, Monday.AddDays(93).AddMinutes(1), "Day"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_EmptyMachineId_ThrowsValidationException()
    {
        // Act
        var act = () => CreateSut().Handle(
            new GetReliabilityTrendRequest(Guid.Empty, Monday, Monday.AddHours(8), "Day"),
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
            new GetReliabilityTrendRequest(machineId, Monday, Monday.AddHours(8), "Day"),
            CancellationToken.None);

        // Assert - cross-tenant machines are invisible through the tenant
        // filter, so they surface here as unknown ids (404, never data)
        await act.Should().ThrowAsync<NotFoundException>();
        _downtimes.Verify(
            d => d.ListOverlappingAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repairs.Verify(
            r => r.ListDoneInWindowAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
