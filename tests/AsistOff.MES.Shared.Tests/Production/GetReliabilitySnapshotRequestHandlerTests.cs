using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Reliability;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class GetReliabilitySnapshotRequestHandlerTests
{
    private static readonly DateTime Monday = new(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IMachinesRepository> _machines = new();
    private readonly Mock<IDowntimeEventsRepository> _downtimes = new();
    private readonly Mock<IMaintenanceWorkOrdersRepository> _repairs = new();

    private GetReliabilitySnapshotRequestHandler CreateSut() => new(
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
        // Assert - the snapshot query must stay tenant-scoped
        typeof(ITenantRequest<ReliabilitySnapshotResponse>)
            .IsAssignableFrom(typeof(GetReliabilitySnapshotRequest)).Should().BeTrue();
        typeof(IAllowAnonymousRequest).IsAssignableFrom(typeof(GetReliabilitySnapshotRequest)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_HappyPath_ReturnsHandComputedValues()
    {
        // Arrange - 8h window (480 min), one 60-min closed stop, one done
        // repair taking 90 min from StartedAt: failureCount 1, downtime 60,
        // uptime 420, mtbf 420, mttr 60, repairs 1, avgRepair 90.
        var machineId = Guid.NewGuid();
        var from = Monday.AddHours(6);
        var to = Monday.AddHours(14);
        ArrangeMachine(machineId);

        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ClosedDowntime(machineId, Monday.AddHours(8), Monday.AddHours(9))]);
        _repairs.Setup(r => r.ListDoneInWindowAsync(
                machineId, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync([DoneRepair(machineId, Monday.AddHours(7), Monday.AddHours(9), Monday.AddHours(10).AddMinutes(30))]);

        // Act
        var result = await CreateSut().Handle(
            new GetReliabilitySnapshotRequest(machineId, from, to), CancellationToken.None);

        // Assert
        result.MachineId.Should().Be(machineId);
        result.FromUtc.Should().Be(from);
        result.ToUtc.Should().Be(to);
        result.FailureCount.Should().Be(1);
        result.RepairCount.Should().Be(1);
        result.WindowMinutes.Should().Be(480);
        result.TotalDowntimeMinutes.Should().Be(60);
        result.UptimeMinutes.Should().Be(420);
        result.MtbfMinutes.Should().Be(420);
        result.MttrMinutes.Should().Be(60);
        result.AvgRepairMinutes.Should().Be(90);
        _downtimes.Verify(d => d.ListOverlappingAsync(
            machineId, from, to, It.IsAny<CancellationToken>()), Times.Once);
        _repairs.Verify(r => r.ListDoneInWindowAsync(
            machineId, from, to, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OpenDowntime_Ignored()
    {
        // Arrange - a still-open stop must not change failure count or downtime
        var machineId = Guid.NewGuid();
        ArrangeMachine(machineId);

        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([OpenDowntime(machineId, Monday.AddHours(7))]);

        // Act
        var result = await CreateSut().Handle(
            new GetReliabilitySnapshotRequest(machineId, Monday.AddHours(6), Monday.AddHours(14)),
            CancellationToken.None);

        // Assert
        result.FailureCount.Should().Be(0);
        result.TotalDowntimeMinutes.Should().Be(0);
        result.MtbfMinutes.Should().BeNull();
        result.MttrMinutes.Should().BeNull();
    }

    [Fact]
    public async Task Handle_NonDoneRepairs_Ignored()
    {
        // Arrange - Open and InProgress rows never count toward repairs
        var machineId = Guid.NewGuid();
        ArrangeMachine(machineId);

        _repairs.Setup(r => r.ListDoneInWindowAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // The handler defensively filters non-Done rows even if the
        // repository ever returned them.
        _repairs.Setup(r => r.ListDoneInWindowAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([OpenRepair(machineId), InProgressRepair(machineId)]);

        // Act
        var result = await CreateSut().Handle(
            new GetReliabilitySnapshotRequest(machineId, Monday.AddHours(6), Monday.AddHours(14)),
            CancellationToken.None);

        // Assert
        result.RepairCount.Should().Be(0);
        result.AvgRepairMinutes.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PartialDowntimeOverlap_CountsOverlapOnly()
    {
        // Arrange - stops spilling outside the window count only with overlap
        var machineId = Guid.NewGuid();
        ArrangeMachine(machineId);

        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                ClosedDowntime(machineId, Monday.AddHours(5), Monday.AddHours(7)),
                ClosedDowntime(machineId, Monday.AddHours(13), Monday.AddHours(15))]);

        // Act
        var result = await CreateSut().Handle(
            new GetReliabilitySnapshotRequest(machineId, Monday.AddHours(6), Monday.AddHours(14)),
            CancellationToken.None);

        // Assert - 60 + 60 overlap, 2 failures
        result.FailureCount.Should().Be(2);
        result.TotalDowntimeMinutes.Should().Be(120);
        result.WindowMinutes.Should().Be(480);
        result.UptimeMinutes.Should().Be(360);
        result.MtbfMinutes.Should().Be(180);
        result.MttrMinutes.Should().Be(60);
    }

    [Fact]
    public async Task Handle_RepairWithoutStartedAt_FallsBackToReportedAt()
    {
        // Arrange - StartedAt null uses CompletedAt minus ReportedAt
        var machineId = Guid.NewGuid();
        ArrangeMachine(machineId);

        _repairs.Setup(r => r.ListDoneInWindowAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                DoneRepair(machineId, Monday.AddHours(9), null, Monday.AddHours(10)),
                DoneRepair(machineId, Monday.AddHours(9), Monday.AddHours(9).AddMinutes(30), Monday.AddHours(10))]);

        // Act
        var result = await CreateSut().Handle(
            new GetReliabilitySnapshotRequest(machineId, Monday.AddHours(6), Monday.AddHours(14)),
            CancellationToken.None);

        // Assert - (60 + 30) / 2 = 45
        result.RepairCount.Should().Be(2);
        result.AvgRepairMinutes.Should().Be(45);
    }

    [Fact]
    public async Task Handle_ZeroFailures_ReturnsNullMtbfMttr_NotZeros()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        ArrangeMachine(machineId);

        // Act
        var result = await CreateSut().Handle(
            new GetReliabilitySnapshotRequest(machineId, Monday.AddHours(6), Monday.AddHours(14)),
            CancellationToken.None);

        // Assert
        result.FailureCount.Should().Be(0);
        result.MtbfMinutes.Should().BeNull();
        result.MttrMinutes.Should().BeNull();
        result.RepairCount.Should().Be(0);
        result.AvgRepairMinutes.Should().BeNull();
        result.WindowMinutes.Should().Be(480);
        result.UptimeMinutes.Should().Be(480);
    }

    [Fact]
    public async Task Handle_ReversedWindow_ThrowsValidationException()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        ArrangeMachine(machineId);

        // Act
        var act = () => CreateSut().Handle(
            new GetReliabilitySnapshotRequest(machineId, Monday.AddHours(14), Monday.AddHours(6)),
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
            new GetReliabilitySnapshotRequest(machineId, Monday, Monday.AddDays(93).AddMinutes(1)),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_WindowExactly93Days_IsAccepted()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        ArrangeMachine(machineId);

        // Act
        var result = await CreateSut().Handle(
            new GetReliabilitySnapshotRequest(machineId, Monday, Monday.AddDays(93)),
            CancellationToken.None);

        // Assert
        result.FailureCount.Should().Be(0);
        result.MtbfMinutes.Should().BeNull();
    }

    [Fact]
    public async Task Handle_EmptyMachineId_ThrowsValidationException()
    {
        // Act
        var act = () => CreateSut().Handle(
            new GetReliabilitySnapshotRequest(Guid.Empty, Monday.AddHours(6), Monday.AddHours(14)),
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
            new GetReliabilitySnapshotRequest(machineId, Monday.AddHours(6), Monday.AddHours(14)),
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
