using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Schedule;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class GetGanttScheduleRequestHandlerTests
{
    private static readonly DateOnly From = new(2026, 9, 21);
    private static readonly DateOnly To = new(2026, 9, 27);
    private static readonly DateTime Now = new(2026, 9, 21, 8, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private readonly Mock<IOperationNodesRepository> _operations = new();
    private readonly Mock<IScheduledOperationsRepository> _overrides = new();
    private readonly Mock<IMachinesRepository> _machines = new();
    private readonly Mock<IDateTimeProvider> _clock = new();

    private GetGanttScheduleRequestHandler CreateSut() => new(
        _orders.Object, _operations.Object, _overrides.Object, _machines.Object, _clock.Object);

    private static ProductionOrder MakeOrder(
        string code,
        Guid? versionId = null,
        ProductionOrderStatus status = ProductionOrderStatus.Released,
        DateTime? dueDate = null,
        decimal planned = 60m) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        ProductId = Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        RecipeVersionId = versionId ?? Guid.NewGuid(),
        PlannedQuantity = planned,
        Status = status,
        DueDate = dueDate,
        CreatedAt = Now
    };

    private static OperationNode MakeNode(
        Guid versionId,
        string code,
        int sortIndex = 0,
        decimal? perUnitSeconds = 60m,
        Guid? machineId = null) => new()
    {
        Id = Guid.NewGuid(),
        RecipeVersionId = versionId,
        Code = code,
        Name = code,
        SortIndex = sortIndex,
        RunTimeMode = RunTimeMode.PerUnitSeconds,
        RunTimePerUnitSeconds = perUnitSeconds,
        ResourceRequirements = machineId.HasValue
            ? [new ResourceRequirement { Id = Guid.NewGuid(), OperationNodeId = Guid.Empty, PreferredMachineId = machineId }]
            : []
    };

    private static Machine MakeMachine(string code) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Name = code,
        CreatedAt = Now
    };

    private void Arrange(
        IReadOnlyCollection<ProductionOrder> orders,
        IReadOnlyCollection<OperationNode> nodes,
        IReadOnlyCollection<ScheduledOperation>? manual = null,
        IReadOnlyCollection<Machine>? machines = null)
    {
        _orders.Setup(r => r.BrowseDispatchBoardAsync(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(orders.ToList());
        _operations.Setup(r => r.ListForVersionsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(nodes.ToList());
        _overrides.Setup(r => r.ListForOrdersAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(manual?.ToList() ?? []);
        _machines.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<Machine>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(machines?.ToList() ?? []);
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
    }

    [Fact]
    public void Request_ImplementsTenantRequest_AndNotAnonymous()
    {
        // Assert - the Gantt query must stay tenant-scoped
        typeof(ITenantRequest<GanttScheduleResponse>)
            .IsAssignableFrom(typeof(GetGanttScheduleRequest)).Should().BeTrue();
        typeof(IAllowAnonymousRequest).IsAssignableFrom(typeof(GetGanttScheduleRequest)).Should().BeFalse();
    }

    [Fact]
    public async Task Validate_InvertedWindow_IsInvalid()
    {
        // Arrange
        var validator = new GetGanttScheduleValidator();

        // Act
        var result = await validator.ValidateAsync(new GetGanttScheduleRequest(To, From, null));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_MissingDates_IsInvalid()
    {
        // Arrange
        var validator = new GetGanttScheduleValidator();

        // Act
        var missingFrom = await validator.ValidateAsync(new GetGanttScheduleRequest(default, To, null));
        var missingTo = await validator.ValidateAsync(new GetGanttScheduleRequest(From, default, null));

        // Assert
        missingFrom.IsValid.Should().BeFalse();
        missingTo.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_Window32Days_IsInvalid_Window31Days_IsValid()
    {
        // Arrange
        var validator = new GetGanttScheduleValidator();

        // Act
        var tooWide = await validator.ValidateAsync(
            new GetGanttScheduleRequest(new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 2), null));
        var maxWide = await validator.ValidateAsync(
            new GetGanttScheduleRequest(new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1), null));

        // Assert
        tooWide.IsValid.Should().BeFalse();
        maxWide.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_HappyPath_GroupsPerWorkCenter_WithTimingMathAndOverdueFlags()
    {
        // Arrange - order due 2026-09-22 12:00 UTC, two chained ops on one
        // Work Center: 60min + 60min backward-anchored to the due date.
        var machine = MakeMachine("WC-10");
        var versionId = Guid.NewGuid();
        var dueDate = new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);
        var order = MakeOrder("PO-GANTT", versionId, dueDate: dueDate);
        var first = MakeNode(versionId, "OP-A", sortIndex: 0, machineId: machine.Id);
        var second = MakeNode(versionId, "OP-B", sortIndex: 1, machineId: machine.Id);
        second.Dependencies.Add(new OperationDependency
        {
            Id = Guid.NewGuid(),
            RecipeVersionId = versionId,
            OperationNodeId = second.Id,
            PredecessorOperationNodeId = first.Id,
            DependencyType = OperationDependencyType.FinishToStart
        });
        Arrange([order], [first, second], machines: [machine]);

        // Act
        var result = await CreateSut().Handle(new GetGanttScheduleRequest(From, To, null), CancellationToken.None);

        // Assert
        result.From.Should().Be(From);
        result.To.Should().Be(To);
        var group = result.Groups.Should().ContainSingle().Subject;
        group.MachineId.Should().Be(machine.Id);
        group.MachineCode.Should().Be("WC-10");
        group.Bars.Should().HaveCount(2);
        var firstBar = group.Bars.Single(b => b.OperationCode == "OP-A");
        firstBar.ProductionOrderCode.Should().Be("PO-GANTT");
        firstBar.PlannedStart.Should().Be(dueDate.AddMinutes(-120));
        firstBar.PlannedEnd.Should().Be(dueDate.AddMinutes(-60));
        var secondBar = group.Bars.Single(b => b.OperationCode == "OP-B");
        secondBar.PlannedStart.Should().Be(dueDate.AddMinutes(-60));
        secondBar.PlannedEnd.Should().Be(dueDate);
        // Backward-anchored chain ends exactly at the due date: not overdue.
        group.Bars.Should().OnlyContain(b => !b.IsOverdue);
    }

    [Fact]
    public async Task Handle_NoDueDate_AnchorsForwardFromClock()
    {
        // Arrange - 60min op with no due date starts at the provider's now.
        var order = MakeOrder("PO-NODUE", dueDate: null);
        var node = MakeNode(order.RecipeVersionId, "OP-A");
        Arrange([order], [node]);

        // Act
        var result = await CreateSut().Handle(new GetGanttScheduleRequest(From, To, null), CancellationToken.None);

        // Assert
        var bar = result.Groups.SelectMany(g => g.Bars).Should().ContainSingle().Subject;
        bar.PlannedStart.Should().Be(Now);
        bar.PlannedEnd.Should().Be(Now.AddMinutes(60));
        bar.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_OverdueOrder_PullsChainIntoWindow_AndFlagsOverdue()
    {
        // Arrange - due 2026-09-10, long before the window: the 60min chain
        // would vanish before the window, so it starts at the window start.
        var order = MakeOrder("PO-LATE", dueDate: new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc));
        var node = MakeNode(order.RecipeVersionId, "OP-A");
        Arrange([order], [node]);

        // Act
        var result = await CreateSut().Handle(new GetGanttScheduleRequest(From, To, null), CancellationToken.None);

        // Assert
        var bar = result.Groups.SelectMany(g => g.Bars).Should().ContainSingle().Subject;
        bar.PlannedStart.Should().Be(From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        bar.PlannedEnd.Should().Be(From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddMinutes(60));
        bar.IsOverdue.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MachineFilter_ReturnsOnlyMatchingLane()
    {
        // Arrange - two ops on different Work Centers.
        var keep = MakeMachine("WC-KEEP");
        var skip = MakeMachine("WC-SKIP");
        var versionId = Guid.NewGuid();
        var dueDate = new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);
        var order = MakeOrder("PO-FILTER", versionId, dueDate: dueDate);
        var first = MakeNode(versionId, "OP-A", sortIndex: 0, machineId: keep.Id);
        var second = MakeNode(versionId, "OP-B", sortIndex: 1, machineId: skip.Id);
        second.Dependencies.Add(new OperationDependency
        {
            Id = Guid.NewGuid(),
            RecipeVersionId = versionId,
            OperationNodeId = second.Id,
            PredecessorOperationNodeId = first.Id,
            DependencyType = OperationDependencyType.FinishToStart
        });
        Arrange([order], [first, second], machines: [keep, skip]);

        // Act
        var result = await CreateSut().Handle(new GetGanttScheduleRequest(From, To, keep.Id), CancellationToken.None);

        // Assert
        var group = result.Groups.Should().ContainSingle().Subject;
        group.MachineId.Should().Be(keep.Id);
        group.Bars.Should().ContainSingle().Which.OperationCode.Should().Be("OP-A");
    }

    [Fact]
    public async Task Handle_ManualOverride_PinsWindowAndLane()
    {
        // Arrange - computed bar would sit at the due date; the override pins
        // it to a fixed window on another Work Center.
        var planned = MakeMachine("WC-PLAN");
        var pinned = MakeMachine("WC-PIN");
        var versionId = Guid.NewGuid();
        var dueDate = new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);
        var order = MakeOrder("PO-OVR", versionId, dueDate: dueDate);
        var node = MakeNode(versionId, "OP-A", machineId: planned.Id);
        var pinnedStart = new DateTime(2026, 9, 23, 8, 0, 0, DateTimeKind.Utc);
        var manual = new ScheduledOperation
        {
            Id = Guid.NewGuid(),
            ProductionOrderId = order.Id,
            OperationNodeId = node.Id,
            MachineId = pinned.Id,
            PlannedStart = pinnedStart,
            PlannedEnd = pinnedStart.AddHours(2),
            CreatedAt = Now
        };
        Arrange([order], [node], manual: [manual], machines: [planned, pinned]);

        // Act
        var result = await CreateSut().Handle(new GetGanttScheduleRequest(From, To, null), CancellationToken.None);

        // Assert
        var group = result.Groups.Should().ContainSingle().Subject;
        group.MachineId.Should().Be(pinned.Id);
        var bar = group.Bars.Should().ContainSingle().Subject;
        bar.MachineId.Should().Be(pinned.Id);
        bar.PlannedStart.Should().Be(pinnedStart);
        bar.PlannedEnd.Should().Be(pinnedStart.AddHours(2));
        // Pinned past the due date: reads overdue.
        bar.IsOverdue.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_IssuesBoundedQuery_WithWindowAndTake200()
    {
        // Arrange
        Arrange([], []);

        // Act
        await CreateSut().Handle(new GetGanttScheduleRequest(From, To, null), CancellationToken.None);

        // Assert - bounded order read issued once; with no orders the
        // handler returns early without further reads.
        _orders.Verify(r => r.BrowseDispatchBoardAsync(From, To, 200, It.IsAny<CancellationToken>()), Times.Once);
        _operations.Verify(r => r.ListForVersionsAsync(
            It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CapsOrderRows_At200()
    {
        // Arrange - defensive in-memory Take guards repositories that ignore
        // the Take; the database applies it first.
        var versionId = Guid.NewGuid();
        var orders = Enumerable.Range(0, 205)
            .Select(i => MakeOrder($"PO-{i:000}", versionId, dueDate: null))
            .ToList();
        var node = MakeNode(versionId, "OP-A");
        Arrange(orders, [node]);

        // Act
        var result = await CreateSut().Handle(new GetGanttScheduleRequest(From, To, null), CancellationToken.None);

        // Assert - 200 orders x 1 overlapping bar each.
        result.Groups.SelectMany(g => g.Bars).Should().HaveCount(200);
    }

    [Fact]
    public async Task Handle_ReversedWindow_ThrowsValidationException_WithoutReads()
    {
        // Arrange
        Arrange([], []);

        // Act
        var act = () => CreateSut().Handle(new GetGanttScheduleRequest(To, From, null), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _orders.Verify(
            r => r.BrowseDispatchBoardAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_MissingDates_ThrowsValidationException_WithoutReads()
    {
        // Arrange
        Arrange([], []);

        // Act
        var act = () => CreateSut().Handle(new GetGanttScheduleRequest(default, To, null), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _orders.Verify(
            r => r.BrowseDispatchBoardAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_Window32Days_ThrowsValidationException_WithoutReads()
    {
        // Arrange
        Arrange([], []);

        // Act
        var act = () => CreateSut().Handle(
            new GetGanttScheduleRequest(new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 2), null),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _orders.Verify(
            r => r.BrowseDispatchBoardAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_Window31Days_Succeeds()
    {
        // Arrange
        Arrange([], []);

        // Act
        var result = await CreateSut().Handle(
            new GetGanttScheduleRequest(new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1), null),
            CancellationToken.None);

        // Assert
        result.Groups.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_OperationWithoutMachine_LandsInUnassignedLane()
    {
        // Arrange
        var order = MakeOrder("PO-UNASSIGNED", dueDate: null);
        var node = MakeNode(order.RecipeVersionId, "OP-A");
        Arrange([order], [node]);

        // Act
        var result = await CreateSut().Handle(new GetGanttScheduleRequest(From, To, null), CancellationToken.None);

        // Assert
        var group = result.Groups.Should().ContainSingle().Subject;
        group.MachineId.Should().BeNull();
        group.MachineCode.Should().BeNull();
        group.Bars.Should().ContainSingle();
    }
}
