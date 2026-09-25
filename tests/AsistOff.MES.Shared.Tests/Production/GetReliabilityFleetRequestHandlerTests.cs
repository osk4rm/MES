using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Reliability;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class GetReliabilityFleetRequestHandlerTests
{
    private static readonly DateTime Monday = new(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IMachinesRepository> _machines = new();
    private readonly Mock<IDepartmentsRepository> _departments = new();
    private readonly Mock<IDowntimeEventsRepository> _downtimes = new();
    private readonly Mock<IMaintenanceWorkOrdersRepository> _repairs = new();

    private GetReliabilityFleetRequestHandler CreateSut() => new(
        _machines.Object, _departments.Object, _downtimes.Object, _repairs.Object);

    private static Machine AMachine(Guid id, string code, bool isActive = true, Guid? departmentId = null) => new()
    {
        Id = id,
        TenantId = Guid.NewGuid(),
        Code = code,
        Name = $"Work Center {code}",
        IsActive = isActive,
        DepartmentId = departmentId
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

    private void ArrangeMachines(params Machine[] machines)
    {
        _machines.Setup(m => m.BrowseAsync(
                It.IsAny<Paginator<Machine>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(machines.ToList());
    }

    private void ArrangeEmptyData(IEnumerable<Guid> machineIds)
    {
        foreach (var id in machineIds)
        {
            var captured = id;
            _downtimes.Setup(d => d.ListOverlappingAsync(
                    captured, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            _repairs.Setup(r => r.ListDoneInWindowAsync(
                    captured, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
        }
    }

    [Fact]
    public void Request_ImplementsTenantRequest_AndNotAnonymous()
    {
        // Assert - the fleet query must stay tenant-scoped
        typeof(ITenantRequest<IReadOnlyList<ReliabilityFleetRowResponse>>)
            .IsAssignableFrom(typeof(GetReliabilityFleetRequest)).Should().BeTrue();
        typeof(IAllowAnonymousRequest).IsAssignableFrom(typeof(GetReliabilityFleetRequest)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ThreeMachines_RanksByMtbfAscendingNullsLast()
    {
        // Arrange - 8h window (480 min): worst 2x60 min stops (mtbf 180),
        // best 1x60 min stop (mtbf 420), clean machine has null MTBF and
        // sorts last.
        var worstId = Guid.NewGuid();
        var bestId = Guid.NewGuid();
        var cleanId = Guid.NewGuid();
        var from = Monday.AddHours(6);
        var to = Monday.AddHours(14);
        ArrangeMachines(
            AMachine(worstId, "WC-3"),
            AMachine(bestId, "WC-1"),
            AMachine(cleanId, "WC-2"));
        ArrangeEmptyData([worstId, bestId, cleanId]);

        _downtimes.Setup(d => d.ListOverlappingAsync(
                worstId, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                ClosedDowntime(worstId, Monday.AddHours(7), Monday.AddHours(8)),
                ClosedDowntime(worstId, Monday.AddHours(9), Monday.AddHours(10))]);
        _downtimes.Setup(d => d.ListOverlappingAsync(
                bestId, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ClosedDowntime(bestId, Monday.AddHours(8), Monday.AddHours(9))]);

        // Act
        var result = await CreateSut().Handle(
            new GetReliabilityFleetRequest(from, to, null), CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
        result[0].MachineId.Should().Be(worstId);
        result[0].FailureCount.Should().Be(2);
        result[0].TotalDowntimeMinutes.Should().Be(120);
        result[0].UptimeMinutes.Should().Be(360);
        result[0].MtbfMinutes.Should().Be(180);
        result[0].MttrMinutes.Should().Be(60);
        result[1].MachineId.Should().Be(bestId);
        result[1].MtbfMinutes.Should().Be(420);
        result[1].MttrMinutes.Should().Be(60);
        result[2].MachineId.Should().Be(cleanId);
        result[2].FailureCount.Should().Be(0);
        result[2].MtbfMinutes.Should().BeNull();
        result[2].MttrMinutes.Should().BeNull();
    }

    [Fact]
    public async Task Handle_SnapshotParity_MatchesSnapshotComputation()
    {
        // Arrange - one 60-min closed stop over an 8h window plus one done
        // repair taking 90 min: failureCount 1, downtime 60, uptime 420,
        // mtbf 420, mttr 60, repairs 1, avgRepair 90.
        var machineId = Guid.NewGuid();
        var from = Monday.AddHours(6);
        var to = Monday.AddHours(14);
        ArrangeMachines(AMachine(machineId, "WC-1"));
        ArrangeEmptyData([machineId]);

        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ClosedDowntime(machineId, Monday.AddHours(8), Monday.AddHours(9))]);
        _repairs.Setup(r => r.ListDoneInWindowAsync(
                machineId, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync([DoneRepair(machineId, Monday.AddHours(7), Monday.AddHours(9), Monday.AddHours(10).AddMinutes(30))]);

        // Act
        var result = await CreateSut().Handle(
            new GetReliabilityFleetRequest(from, to, null), CancellationToken.None);

        // Assert
        var row = result.Should().ContainSingle().Subject;
        row.MachineId.Should().Be(machineId);
        row.FailureCount.Should().Be(1);
        row.RepairCount.Should().Be(1);
        row.WindowMinutes.Should().Be(480);
        row.TotalDowntimeMinutes.Should().Be(60);
        row.UptimeMinutes.Should().Be(420);
        row.MtbfMinutes.Should().Be(420);
        row.MttrMinutes.Should().Be(60);
        row.AvgRepairMinutes.Should().Be(90);
    }

    [Fact]
    public async Task Handle_OpenDowntimePerMachine_Ignored()
    {
        // Arrange - a still-open stop must not change failure count or downtime
        var machineId = Guid.NewGuid();
        ArrangeMachines(AMachine(machineId, "WC-1"));
        ArrangeEmptyData([machineId]);

        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([OpenDowntime(machineId, Monday.AddHours(7))]);

        // Act
        var result = await CreateSut().Handle(
            new GetReliabilityFleetRequest(Monday.AddHours(6), Monday.AddHours(14), null),
            CancellationToken.None);

        // Assert
        var row = result.Should().ContainSingle().Subject;
        row.FailureCount.Should().Be(0);
        row.TotalDowntimeMinutes.Should().Be(0);
        row.MtbfMinutes.Should().BeNull();
        row.MttrMinutes.Should().BeNull();
    }

    [Fact]
    public async Task Handle_NonDoneRepairs_Ignored()
    {
        // Arrange - Open rows never count toward repairs even if returned
        var machineId = Guid.NewGuid();
        ArrangeMachines(AMachine(machineId, "WC-1"));
        ArrangeEmptyData([machineId]);

        _repairs.Setup(r => r.ListDoneInWindowAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([OpenRepair(machineId)]);

        // Act
        var result = await CreateSut().Handle(
            new GetReliabilityFleetRequest(Monday.AddHours(6), Monday.AddHours(14), null),
            CancellationToken.None);

        // Assert
        var row = result.Should().ContainSingle().Subject;
        row.RepairCount.Should().Be(0);
        row.AvgRepairMinutes.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PartialOverlapPerMachine_CountsOverlapOnly()
    {
        // Arrange - stops spilling outside the window count only with overlap
        var machineId = Guid.NewGuid();
        ArrangeMachines(AMachine(machineId, "WC-1"));
        ArrangeEmptyData([machineId]);

        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                ClosedDowntime(machineId, Monday.AddHours(5), Monday.AddHours(7)),
                ClosedDowntime(machineId, Monday.AddHours(13), Monday.AddHours(15))]);

        // Act
        var result = await CreateSut().Handle(
            new GetReliabilityFleetRequest(Monday.AddHours(6), Monday.AddHours(14), null),
            CancellationToken.None);

        // Assert - 60 + 60 overlap, 2 failures
        var row = result.Should().ContainSingle().Subject;
        row.FailureCount.Should().Be(2);
        row.TotalDowntimeMinutes.Should().Be(120);
        row.MtbfMinutes.Should().Be(180);
        row.MttrMinutes.Should().Be(60);
    }

    [Fact]
    public async Task Handle_InactiveMachines_Excluded()
    {
        // Arrange
        var activeId = Guid.NewGuid();
        var inactiveId = Guid.NewGuid();
        ArrangeMachines(
            AMachine(activeId, "WC-1", isActive: true),
            AMachine(inactiveId, "WC-2", isActive: false));
        ArrangeEmptyData([activeId, inactiveId]);

        // Act
        var result = await CreateSut().Handle(
            new GetReliabilityFleetRequest(Monday.AddHours(6), Monday.AddHours(14), null),
            CancellationToken.None);

        // Assert - even though the mocked repository returns the inactive
        // row, the handler filters it out
        result.Should().ContainSingle();
        result[0].MachineId.Should().Be(activeId);
    }

    [Fact]
    public async Task Handle_DepartmentFilter_ReturnsOnlyMatchingMachines()
    {
        // Arrange
        var departmentId = Guid.NewGuid();
        var otherDepartmentId = Guid.NewGuid();
        var wantedId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var unassignedId = Guid.NewGuid();
        ArrangeMachines(
            AMachine(wantedId, "WC-1", departmentId: departmentId),
            AMachine(otherId, "WC-2", departmentId: otherDepartmentId),
            AMachine(unassignedId, "WC-3", departmentId: null));
        ArrangeEmptyData([wantedId, otherId, unassignedId]);

        _departments.Setup(d => d.GetByIdAsync(departmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Department { Id = departmentId, TenantId = Guid.NewGuid(), Code = "D1", Name = "Dept 1" });

        // Act
        var result = await CreateSut().Handle(
            new GetReliabilityFleetRequest(Monday.AddHours(6), Monday.AddHours(14), departmentId),
            CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        result[0].MachineId.Should().Be(wantedId);
        result[0].DepartmentId.Should().Be(departmentId);
    }

    [Fact]
    public async Task Handle_UnknownDepartment_ThrowsNotFoundException_WithoutTouchingData()
    {
        // Arrange
        var departmentId = Guid.NewGuid();
        _departments.Setup(d => d.GetByIdAsync(departmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Department?)null);

        // Act
        var act = () => CreateSut().Handle(
            new GetReliabilityFleetRequest(Monday.AddHours(6), Monday.AddHours(14), departmentId),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _machines.Verify(
            m => m.BrowseAsync(It.IsAny<Paginator<Machine>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ReversedWindow_ThrowsValidationException()
    {
        // Act
        var act = () => CreateSut().Handle(
            new GetReliabilityFleetRequest(Monday.AddHours(14), Monday.AddHours(6), null),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_WindowOver93Days_ThrowsValidationException()
    {
        // Act
        var act = () => CreateSut().Handle(
            new GetReliabilityFleetRequest(Monday, Monday.AddDays(93).AddMinutes(1), null),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_EmptyDepartmentId_ThrowsValidationException()
    {
        // Act
        var act = () => CreateSut().Handle(
            new GetReliabilityFleetRequest(Monday.AddHours(6), Monday.AddHours(14), Guid.Empty),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _machines.Verify(
            m => m.BrowseAsync(It.IsAny<Paginator<Machine>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
