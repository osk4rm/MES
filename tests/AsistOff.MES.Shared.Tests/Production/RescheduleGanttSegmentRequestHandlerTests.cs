using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Schedule;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Users.Core.Rbac;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class RescheduleGanttSegmentRequestHandlerTests
{
    private static readonly DateTime WindowStart = new(2027, 5, 11, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowEnd = WindowStart.AddHours(2);

    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private readonly Mock<IOperationNodesRepository> _operations = new();
    private readonly Mock<IScheduledOperationsRepository> _scheduled = new();
    private readonly Mock<IMachinesRepository> _machines = new();
    private readonly Mock<IShiftsRepository> _shifts = new();
    private readonly Mock<IOperatorShiftAssignmentsRepository> _roster = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _overrideId = Guid.NewGuid();

    public RescheduleGanttSegmentRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(_overrideId);
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);

        // No shifts and no roster by default: every move warns unless a test
        // arranges coverage.
        _shifts.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<Shift>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _roster.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<OperatorShiftAssignment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _scheduled.Setup(r => r.ListForMachineAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _scheduled.Setup(r => r.ListForOrdersAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    private RescheduleGanttSegmentRequestHandler CreateSut() => new(
        _orders.Object, _operations.Object, _scheduled.Object, _machines.Object,
        _shifts.Object, _roster.Object, _guids.Object, _tenant.Object);

    private static ProductionOrder MakeOrder(Guid versionId, uint xmin = 7) => new()
    {
        Id = Guid.NewGuid(),
        Code = "PO-MOVE",
        ProductId = Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        RecipeVersionId = versionId,
        PlannedQuantity = 60m,
        Status = ProductionOrderStatus.Released,
        Xmin = xmin,
        CreatedAt = WindowStart
    };

    private static OperationNode MakeNode(Guid versionId) => new()
    {
        Id = Guid.NewGuid(),
        RecipeVersionId = versionId,
        Code = "OP-A",
        Name = "Operation A"
    };

    private static Machine MakeMachine(string code) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Name = code,
        CreatedAt = WindowStart
    };

    private static Shift MakeShift() => new()
    {
        Id = Guid.NewGuid(),
        Code = "S1",
        Name = "Shift 1",
        StartTime = new TimeOnly(6, 0),
        EndTime = new TimeOnly(14, 0),
        IsActive = true
    };

    private RescheduleGanttSegmentRequest ValidRequest(
        ProductionOrder order, OperationNode node, Machine machine, string? token = null) => new(
        node.Id, order.Id, WindowStart, WindowEnd, machine.Id, token ?? order.Xmin.ToString(), false, null);

    private void ArrangeOrderGraph(ProductionOrder order, OperationNode node, Machine machine)
    {
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _operations.Setup(r => r.GetAsync(node.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(node);
        _machines.Setup(r => r.GetByIdAsync(machine.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(machine);
    }

    [Fact]
    public void Request_ImplementsTenantRequest_AndNotAnonymous_AndRequiresProductionWrite()
    {
        // Assert - the reschedule write must stay tenant-scoped behind production.write
        typeof(ITenantRequest<RescheduleGanttSegmentResponse>)
            .IsAssignableFrom(typeof(RescheduleGanttSegmentRequest)).Should().BeTrue();
        typeof(IAllowAnonymousRequest).IsAssignableFrom(typeof(RescheduleGanttSegmentRequest)).Should().BeFalse();
        typeof(RescheduleGanttSegmentRequest)
            .GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true)
            .Cast<RequirePermissionAttribute>()
            .Select(a => a.Permission)
            .Should().ContainSingle().Which.Should().Be(RbacDefaults.ProductionWrite);
    }

    [Fact]
    public async Task Handle_FirstMove_CreatesOverride_WithTenantWindowAndLane()
    {
        // Arrange
        var versionId = Guid.NewGuid();
        var order = MakeOrder(versionId);
        var node = MakeNode(versionId);
        var machine = MakeMachine("WC-10");
        ArrangeOrderGraph(order, node, machine);

        ScheduledOperation? saved = null;
        _scheduled.Setup(r => r.AddAsync(It.IsAny<ScheduledOperation>(), It.IsAny<CancellationToken>()))
            .Callback<ScheduledOperation, CancellationToken>((e, _) => saved = e)
            .ReturnsAsync((ScheduledOperation e, CancellationToken _) => e);

        // Act
        var result = await CreateSut().Handle(ValidRequest(order, node, machine), CancellationToken.None);

        // Assert
        saved.Should().NotBeNull();
        saved!.Id.Should().Be(_overrideId);
        saved.TenantId.Should().Be(_tenantId);
        saved.ProductionOrderId.Should().Be(order.Id);
        saved.OperationNodeId.Should().Be(node.Id);
        saved.MachineId.Should().Be(machine.Id);
        saved.PlannedStart.Should().Be(WindowStart);
        saved.PlannedEnd.Should().Be(WindowEnd);

        result.Id.Should().Be(_overrideId);
        result.ProductionOrderId.Should().Be(order.Id);
        result.OperationNodeId.Should().Be(node.Id);
        result.MachineId.Should().Be(machine.Id);
        result.PlannedStart.Should().Be(WindowStart);
        result.PlannedEnd.Should().Be(WindowEnd);
        result.ConflictingSegmentIds.Should().BeEmpty();
        // No shifts and no roster arranged: advisory warning is raised.
        result.ShiftCoverageWarning.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_CoveredWindow_ReturnsNoWarning()
    {
        // Arrange - one active shift with one rostered operator on the move date.
        var versionId = Guid.NewGuid();
        var order = MakeOrder(versionId);
        var node = MakeNode(versionId);
        var machine = MakeMachine("WC-10");
        ArrangeOrderGraph(order, node, machine);
        var shift = MakeShift();
        _shifts.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<Shift>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([shift]);
        _roster.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<OperatorShiftAssignment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OperatorShiftAssignment
            {
                Id = Guid.NewGuid(),
                OperatorId = Guid.NewGuid(),
                ShiftId = shift.Id,
                Date = DateOnly.FromDateTime(WindowStart)
            }]);
        _scheduled.Setup(r => r.AddAsync(It.IsAny<ScheduledOperation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduledOperation e, CancellationToken _) => e);

        // Act
        var result = await CreateSut().Handle(ValidRequest(order, node, machine), CancellationToken.None);

        // Assert
        result.ShiftCoverageWarning.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SecondMove_UpdatesExistingOverride()
    {
        // Arrange - the segment was moved before; the row is reused, not duplicated.
        var versionId = Guid.NewGuid();
        var order = MakeOrder(versionId);
        var node = MakeNode(versionId);
        var oldMachine = MakeMachine("WC-OLD");
        var newMachine = MakeMachine("WC-NEW");
        ArrangeOrderGraph(order, node, newMachine);
        var existing = new ScheduledOperation
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ProductionOrderId = order.Id,
            OperationNodeId = node.Id,
            MachineId = oldMachine.Id,
            PlannedStart = WindowStart.AddDays(-1),
            PlannedEnd = WindowEnd.AddDays(-1),
            CreatedAt = WindowStart
        };
        _scheduled.Setup(r => r.ListForOrdersAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([existing]);

        // Act
        var result = await CreateSut().Handle(ValidRequest(order, node, newMachine), CancellationToken.None);

        // Assert
        _scheduled.Verify(r => r.AddAsync(It.IsAny<ScheduledOperation>(), It.IsAny<CancellationToken>()), Times.Never);
        _scheduled.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        existing.MachineId.Should().Be(newMachine.Id);
        existing.PlannedStart.Should().Be(WindowStart);
        existing.PlannedEnd.Should().Be(WindowEnd);
        result.Id.Should().Be(existing.Id);
        result.MachineId.Should().Be(newMachine.Id);
    }

    [Fact]
    public async Task Handle_OverlappingMove_ThrowsConflict_ListingSegmentIds_AndPersistsNothing()
    {
        // Arrange - another order's override already occupies the lane and window.
        var versionId = Guid.NewGuid();
        var order = MakeOrder(versionId);
        var node = MakeNode(versionId);
        var machine = MakeMachine("WC-10");
        ArrangeOrderGraph(order, node, machine);
        var blocking = new ScheduledOperation
        {
            Id = Guid.NewGuid(),
            ProductionOrderId = Guid.NewGuid(),
            OperationNodeId = Guid.NewGuid(),
            MachineId = machine.Id,
            PlannedStart = WindowStart.AddHours(1),
            PlannedEnd = WindowEnd.AddHours(1),
            CreatedAt = WindowStart
        };
        _scheduled.Setup(r => r.ListForMachineAsync(
                machine.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([blocking]);

        // Act
        var act = () => CreateSut().Handle(ValidRequest(order, node, machine), CancellationToken.None);

        // Assert - a ConflictException (HTTP 409) carrying the blocking ids.
        var thrown = await act.Should().ThrowAsync<GanttScheduleConflictException>();
        thrown.Which.ConflictingSegmentIds.Should().ContainSingle().Which.Should().Be(blocking.Id);
        thrown.Which.Should().BeAssignableTo<ConflictException>();
        _scheduled.Verify(r => r.AddAsync(It.IsAny<ScheduledOperation>(), It.IsAny<CancellationToken>()), Times.Never);
        _scheduled.Verify(r => r.UpdateAsync(It.IsAny<ScheduledOperation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OverlappingMove_WithForce_Persists_AndEchoesBypassedIds()
    {
        // Arrange
        var versionId = Guid.NewGuid();
        var order = MakeOrder(versionId);
        var node = MakeNode(versionId);
        var machine = MakeMachine("WC-10");
        ArrangeOrderGraph(order, node, machine);
        var blocking = new ScheduledOperation
        {
            Id = Guid.NewGuid(),
            ProductionOrderId = Guid.NewGuid(),
            OperationNodeId = Guid.NewGuid(),
            MachineId = machine.Id,
            PlannedStart = WindowStart.AddHours(1),
            PlannedEnd = WindowEnd.AddHours(1),
            CreatedAt = WindowStart
        };
        _scheduled.Setup(r => r.ListForMachineAsync(
                machine.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([blocking]);
        _scheduled.Setup(r => r.AddAsync(It.IsAny<ScheduledOperation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduledOperation e, CancellationToken _) => e);
        var request = ValidRequest(order, node, machine) with { Force = true };

        // Act
        var result = await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        _scheduled.Verify(r => r.AddAsync(It.IsAny<ScheduledOperation>(), It.IsAny<CancellationToken>()), Times.Once);
        result.ConflictingSegmentIds.Should().ContainSingle().Which.Should().Be(blocking.Id);
    }

    [Fact]
    public async Task Handle_TouchingEdges_DoNotConflict()
    {
        // Arrange - the existing override ends exactly when the move starts:
        // half-open intervals share no minute.
        var versionId = Guid.NewGuid();
        var order = MakeOrder(versionId);
        var node = MakeNode(versionId);
        var machine = MakeMachine("WC-10");
        ArrangeOrderGraph(order, node, machine);
        _scheduled.Setup(r => r.ListForMachineAsync(
                machine.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ScheduledOperation
            {
                Id = Guid.NewGuid(),
                ProductionOrderId = Guid.NewGuid(),
                OperationNodeId = Guid.NewGuid(),
                MachineId = machine.Id,
                PlannedStart = WindowStart.AddHours(-2),
                PlannedEnd = WindowStart,
                CreatedAt = WindowStart
            }]);
        _scheduled.Setup(r => r.AddAsync(It.IsAny<ScheduledOperation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduledOperation e, CancellationToken _) => e);

        // Act
        var result = await CreateSut().Handle(ValidRequest(order, node, machine), CancellationToken.None);

        // Assert
        result.ConflictingSegmentIds.Should().BeEmpty();
        _scheduled.Verify(r => r.AddAsync(It.IsAny<ScheduledOperation>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OwnExistingOverride_NeverConflictsWithItself()
    {
        // Arrange - re-saving the same segment over its own persisted window.
        var versionId = Guid.NewGuid();
        var order = MakeOrder(versionId);
        var node = MakeNode(versionId);
        var machine = MakeMachine("WC-10");
        ArrangeOrderGraph(order, node, machine);
        var own = new ScheduledOperation
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ProductionOrderId = order.Id,
            OperationNodeId = node.Id,
            MachineId = machine.Id,
            PlannedStart = WindowStart,
            PlannedEnd = WindowEnd,
            CreatedAt = WindowStart
        };
        _scheduled.Setup(r => r.ListForMachineAsync(
                machine.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([own]);
        _scheduled.Setup(r => r.ListForOrdersAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([own]);

        // Act
        var result = await CreateSut().Handle(ValidRequest(order, node, machine), CancellationToken.None);

        // Assert
        result.Id.Should().Be(own.Id);
        _scheduled.Verify(r => r.UpdateAsync(own, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_StaleToken_ThrowsConcurrencyConflict_WithCurrentToken()
    {
        // Arrange
        var versionId = Guid.NewGuid();
        var order = MakeOrder(versionId, xmin: 7);
        var node = MakeNode(versionId);
        var machine = MakeMachine("WC-10");
        ArrangeOrderGraph(order, node, machine);
        var request = ValidRequest(order, node, machine, token: "0");

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert - 409 with retry guidance and the current token.
        var thrown = await act.Should().ThrowAsync<ConcurrencyConflictException>();
        thrown.Which.CurrentToken.Should().Be("7");
        _scheduled.Verify(r => r.AddAsync(It.IsAny<ScheduledOperation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MissingToken_ThrowsValidationException()
    {
        // Arrange
        var versionId = Guid.NewGuid();
        var order = MakeOrder(versionId);
        var node = MakeNode(versionId);
        var machine = MakeMachine("WC-10");
        ArrangeOrderGraph(order, node, machine);
        var request = ValidRequest(order, node, machine, token: null) with { ConcurrencyToken = " " };

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_StartAfterEnd_ThrowsValidationException_WithoutReads()
    {
        // Arrange
        var versionId = Guid.NewGuid();
        var order = MakeOrder(versionId);
        var node = MakeNode(versionId);
        var machine = MakeMachine("WC-10");
        var request = ValidRequest(order, node, machine) with
        {
            PlannedStart = WindowEnd,
            PlannedEnd = WindowStart
        };

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _orders.Verify(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WindowOver31Days_ThrowsValidationException()
    {
        // Arrange
        var versionId = Guid.NewGuid();
        var order = MakeOrder(versionId);
        var node = MakeNode(versionId);
        var machine = MakeMachine("WC-10");
        var request = ValidRequest(order, node, machine) with
        {
            PlannedEnd = WindowStart.AddDays(32)
        };

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_UnknownOrder_ThrowsNotFound()
    {
        // Arrange
        var versionId = Guid.NewGuid();
        var order = MakeOrder(versionId);
        var node = MakeNode(versionId);
        var machine = MakeMachine("WC-10");
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductionOrder?)null);

        // Act
        var act = () => CreateSut().Handle(ValidRequest(order, node, machine), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_UnknownOperation_ThrowsNotFound()
    {
        // Arrange
        var versionId = Guid.NewGuid();
        var order = MakeOrder(versionId);
        var node = MakeNode(versionId);
        var machine = MakeMachine("WC-10");
        ArrangeOrderGraph(order, node, machine);
        _operations.Setup(r => r.GetAsync(node.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperationNode?)null);

        // Act
        var act = () => CreateSut().Handle(ValidRequest(order, node, machine), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_OperationFromAnotherVersion_ThrowsNotFound()
    {
        // Arrange - the operation exists (same tenant) but belongs to a
        // different recipe version than the order: it cannot be moved here.
        var versionId = Guid.NewGuid();
        var order = MakeOrder(versionId);
        var foreign = MakeNode(Guid.NewGuid());
        var machine = MakeMachine("WC-10");
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _operations.Setup(r => r.GetAsync(foreign.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(foreign);
        _machines.Setup(r => r.GetByIdAsync(machine.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(machine);

        // Act
        var act = () => CreateSut().Handle(ValidRequest(order, foreign, machine), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _scheduled.Verify(r => r.AddAsync(It.IsAny<ScheduledOperation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownMachine_ThrowsNotFound_AndPersistsNothing()
    {
        // Arrange - unknown and cross-tenant machines both surface as null
        // under the tenant query filter.
        var versionId = Guid.NewGuid();
        var order = MakeOrder(versionId);
        var node = MakeNode(versionId);
        var machine = MakeMachine("WC-10");
        ArrangeOrderGraph(order, node, machine);
        _machines.Setup(r => r.GetByIdAsync(machine.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Machine?)null);

        // Act
        var act = () => CreateSut().Handle(ValidRequest(order, node, machine), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _scheduled.Verify(r => r.AddAsync(It.IsAny<ScheduledOperation>(), It.IsAny<CancellationToken>()), Times.Never);
        _scheduled.Verify(r => r.UpdateAsync(It.IsAny<ScheduledOperation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShiftsWithoutRoster_ReturnsCoverageWarning()
    {
        // Arrange - active shifts exist but nobody is rostered on the date.
        var versionId = Guid.NewGuid();
        var order = MakeOrder(versionId);
        var node = MakeNode(versionId);
        var machine = MakeMachine("WC-10");
        ArrangeOrderGraph(order, node, machine);
        _shifts.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<Shift>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeShift()]);
        _scheduled.Setup(r => r.AddAsync(It.IsAny<ScheduledOperation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduledOperation e, CancellationToken _) => e);

        // Act
        var result = await CreateSut().Handle(ValidRequest(order, node, machine), CancellationToken.None);

        // Assert
        result.ShiftCoverageWarning.Should().BeTrue();
    }
}
