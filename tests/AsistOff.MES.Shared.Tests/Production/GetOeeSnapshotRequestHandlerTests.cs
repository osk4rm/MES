using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Oee.Snapshot;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class GetOeeSnapshotRequestHandlerTests
{
    private static readonly DateTime Monday = new(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IMachinesRepository> _machines = new();
    private readonly Mock<IWorkCenterCalendarsRepository> _calendars = new();
    private readonly Mock<IDowntimeEventsRepository> _downtimes = new();
    private readonly Mock<IProductionConfirmationsRepository> _confirmations = new();

    private GetOeeSnapshotRequestHandler CreateSut() => new(
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

    private static DowntimeEvent OpenDowntime(Guid machineId, DateTime start) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        MachineId = machineId,
        ReasonCodeId = Guid.NewGuid(),
        StartedAt = start,
        EndedAt = null
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
        // Assert - the snapshot query must stay tenant-scoped
        typeof(ITenantRequest<OeeSnapshotResponse>)
            .IsAssignableFrom(typeof(GetOeeSnapshotRequest)).Should().BeTrue();
        typeof(IAllowAnonymousRequest).IsAssignableFrom(typeof(GetOeeSnapshotRequest)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_HappyPath_ReturnsHandComputedFactors()
    {
        // Arrange - Monday 06:00-14:00 window, 8h planned, 1h closed stop,
        // 90 good + 10 scrap, ideal cycle 60s:
        // availability = 420/480 = 0.875, performance = 100/420 = 0.2381,
        // quality = 90/100 = 0.9, oee = 0.875*0.2381*0.9 = 0.1875
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
            new GetOeeSnapshotRequest(machineId, from, to, 60m), CancellationToken.None);

        // Assert
        result.MachineId.Should().Be(machineId);
        result.FromUtc.Should().Be(from);
        result.ToUtc.Should().Be(to);
        result.IdealCycleTimeSeconds.Should().Be(60m);
        result.PlannedProductionTimeMinutes.Should().Be(480);
        result.DowntimeMinutes.Should().Be(60);
        result.RunTimeMinutes.Should().Be(420);
        result.TotalCount.Should().Be(100m);
        result.GoodCount.Should().Be(90m);
        result.ScrapCount.Should().Be(10m);
        result.Availability.Should().Be(0.875);
        result.Performance.Should().Be(0.2381);
        result.Quality.Should().Be(0.9);
        result.Oee.Should().Be(0.1875);
        result.AvailabilityComputed.Should().BeTrue();
        result.PerformanceComputed.Should().BeTrue();
        result.QualityComputed.Should().BeTrue();
        _downtimes.Verify(d => d.ListOverlappingAsync(
            machineId, from, to, It.IsAny<CancellationToken>()), Times.Once);
        _confirmations.Verify(c => c.ListForMachineInWindowAsync(
            machineId, from, to, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoCalendar_ReturnsNullFactorsNotZeros()
    {
        // Arrange
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
            new GetOeeSnapshotRequest(machineId, Monday.AddHours(6), Monday.AddHours(14), 60m),
            CancellationToken.None);

        // Assert
        result.Availability.Should().BeNull();
        result.Performance.Should().BeNull();
        result.Quality.Should().BeNull();
        result.Oee.Should().BeNull();
        result.AvailabilityComputed.Should().BeFalse();
        result.PerformanceComputed.Should().BeFalse();
        result.QualityComputed.Should().BeFalse();
        result.PlannedProductionTimeMinutes.Should().Be(0);
        result.TotalCount.Should().Be(10m);
    }

    [Fact]
    public async Task Handle_NoOverlappingEntries_ReturnsNullFactors()
    {
        // Arrange - calendar covers Tuesday only, window is Monday
        var machineId = Guid.NewGuid();
        ArrangeMachineWithCalendar(machineId, Entry(DayOfWeek.Tuesday, "06:00", "14:00"));

        // Act
        var result = await CreateSut().Handle(
            new GetOeeSnapshotRequest(machineId, Monday.AddHours(6), Monday.AddHours(14), 60m),
            CancellationToken.None);

        // Assert
        result.PlannedProductionTimeMinutes.Should().Be(0);
        result.Availability.Should().BeNull();
        result.Performance.Should().BeNull();
        result.Quality.Should().BeNull();
        result.Oee.Should().BeNull();
    }

    [Fact]
    public async Task Handle_NoConfirmations_AvailabilityComputed_PerformanceQualityNull()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        ArrangeMachineWithCalendar(machineId, Entry(Monday.DayOfWeek, "06:00", "14:00"));

        // Act
        var result = await CreateSut().Handle(
            new GetOeeSnapshotRequest(machineId, Monday.AddHours(6), Monday.AddHours(14), 60m),
            CancellationToken.None);

        // Assert
        result.Availability.Should().Be(1.0);
        result.AvailabilityComputed.Should().BeTrue();
        result.Performance.Should().BeNull();
        result.Quality.Should().BeNull();
        result.Oee.Should().BeNull();
    }

    [Fact]
    public async Task Handle_FullWindowDowntime_RunZero_AvailabilityPerformanceNull_QualityComputed()
    {
        // Arrange - the stop covers the whole planned window
        var machineId = Guid.NewGuid();
        ArrangeMachineWithCalendar(machineId, Entry(Monday.DayOfWeek, "06:00", "14:00"));

        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([ClosedDowntime(machineId, Monday.AddHours(6), Monday.AddHours(14))]);
        _confirmations.Setup(c => c.ListForMachineInWindowAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Confirmation(machineId, Monday.AddHours(10), 90m, 10m)]);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeSnapshotRequest(machineId, Monday.AddHours(6), Monday.AddHours(14), 60m),
            CancellationToken.None);

        // Assert
        result.RunTimeMinutes.Should().Be(0);
        result.Availability.Should().BeNull();
        result.Performance.Should().BeNull();
        result.Quality.Should().Be(0.9);
        result.QualityComputed.Should().BeTrue();
        result.Oee.Should().BeNull();
    }

    [Fact]
    public async Task Handle_OpenDowntime_Ignored()
    {
        // Arrange - a still-open stop spanning the window must not reduce run time
        var machineId = Guid.NewGuid();
        ArrangeMachineWithCalendar(machineId, Entry(Monday.DayOfWeek, "06:00", "14:00"));

        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                OpenDowntime(machineId, Monday.AddHours(7)),
                ClosedDowntime(machineId, Monday.AddHours(9), Monday.AddHours(9).AddMinutes(30))]);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeSnapshotRequest(machineId, Monday.AddHours(6), Monday.AddHours(14), 60m),
            CancellationToken.None);

        // Assert
        result.DowntimeMinutes.Should().Be(30);
        result.RunTimeMinutes.Should().Be(450);
        result.Availability.Should().Be(0.9375);
    }

    [Fact]
    public async Task Handle_PartialDowntimeOverlap_CountsOverlapOnly()
    {
        // Arrange - stops spilling outside the window count only with their overlap
        var machineId = Guid.NewGuid();
        ArrangeMachineWithCalendar(machineId, Entry(Monday.DayOfWeek, "06:00", "14:00"));

        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                ClosedDowntime(machineId, Monday.AddHours(5), Monday.AddHours(7)),
                ClosedDowntime(machineId, Monday.AddHours(13), Monday.AddHours(15))]);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeSnapshotRequest(machineId, Monday.AddHours(6), Monday.AddHours(14), 60m),
            CancellationToken.None);

        // Assert
        result.DowntimeMinutes.Should().Be(120);
        result.RunTimeMinutes.Should().Be(360);
    }

    [Fact]
    public async Task Handle_OvernightCalendarEntry_CountsSpillIntoWindow()
    {
        // Arrange - Sunday 22:00-06:00 spills 6h into the Monday 00:00-08:00 window
        var machineId = Guid.NewGuid();
        ArrangeMachineWithCalendar(machineId, Entry(DayOfWeek.Sunday, "22:00", "06:00"));

        // Act
        var result = await CreateSut().Handle(
            new GetOeeSnapshotRequest(machineId, Monday, Monday.AddHours(8), 60m),
            CancellationToken.None);

        // Assert
        result.PlannedProductionTimeMinutes.Should().Be(360);
        result.RunTimeMinutes.Should().Be(360);
        result.Availability.Should().Be(1.0);
    }

    [Fact]
    public async Task Handle_NonWorkingEntries_ExcludedFromPlannedTime()
    {
        // Arrange - a planned break is not production time
        var machineId = Guid.NewGuid();
        ArrangeMachineWithCalendar(
            machineId,
            Entry(Monday.DayOfWeek, "06:00", "14:00", isWorking: false),
            Entry(Monday.DayOfWeek, "14:00", "15:00"));

        // Act
        var result = await CreateSut().Handle(
            new GetOeeSnapshotRequest(machineId, Monday.AddHours(6), Monday.AddHours(15), 60m),
            CancellationToken.None);

        // Assert
        result.PlannedProductionTimeMinutes.Should().Be(60);
    }

    [Fact]
    public async Task Handle_WindowExactly93Days_IsAccepted()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        ArrangeMachineWithCalendar(machineId);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeSnapshotRequest(machineId, Monday, Monday.AddDays(93), 60m),
            CancellationToken.None);

        // Assert
        result.Oee.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-60)]
    public async Task Handle_NonPositiveIdealCycleTime_ThrowsValidationException(decimal ideal)
    {
        // Arrange
        var machineId = Guid.NewGuid();
        ArrangeMachineWithCalendar(machineId);

        // Act
        var act = () => CreateSut().Handle(
            new GetOeeSnapshotRequest(machineId, Monday.AddHours(6), Monday.AddHours(14), ideal),
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
            new GetOeeSnapshotRequest(machineId, Monday.AddHours(14), Monday.AddHours(6), 60m),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_EqualBoundsWindow_ThrowsValidationException()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        ArrangeMachineWithCalendar(machineId);

        // Act
        var act = () => CreateSut().Handle(
            new GetOeeSnapshotRequest(machineId, Monday.AddHours(6), Monday.AddHours(6), 60m),
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
            new GetOeeSnapshotRequest(machineId, Monday, Monday.AddDays(93).AddMinutes(1), 60m),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_EmptyMachineId_ThrowsValidationException()
    {
        // Act
        var act = () => CreateSut().Handle(
            new GetOeeSnapshotRequest(Guid.Empty, Monday.AddHours(6), Monday.AddHours(14), 60m),
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
            new GetOeeSnapshotRequest(machineId, Monday.AddHours(6), Monday.AddHours(14), 60m),
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
