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

public class GetOperatorShiftQueueRequestHandlerTests
{
    private static readonly DateOnly ShiftDate = new(2026, 9, 24);
    private static readonly DateTime NowInShift = new(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IOperatorsRepository> _operators = new();
    private readonly Mock<IOperatorShiftAssignmentsRepository> _assignments = new();
    private readonly Mock<IShiftsRepository> _shifts = new();
    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private readonly Mock<IProductionConfirmationsRepository> _confirmations = new();
    private readonly Mock<IScheduledOperationsRepository> _scheduledOps = new();
    private readonly Mock<IMachinesRepository> _machines = new();
    private readonly Mock<IProductsRepository> _products = new();
    private readonly Mock<IAndonSignalsRepository> _andon = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IOperationNodesRepository> _operationNodes = new();
    private readonly Mock<ISkillsRepository> _skills = new();
    private readonly Mock<IOperatorSkillQualificationsRepository> _qualifications = new();

    public GetOperatorShiftQueueRequestHandlerTests()
    {
        // Skill-gap flags (issue #397): no skill requirements and no held
        // skills by default, so existing queue scenarios stay unflagged.
        _operationNodes.Setup(r => r.ListForVersionsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperationNode>());
        _skills.Setup(r => r.ListByCodesAsync(
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Skill>());
        _qualifications.Setup(r => r.ListSkillCodesForOperatorsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyCollection<string>>());
    }

    private GetOperatorShiftQueueRequestHandler CreateSut() => new(
        _operators.Object,
        _assignments.Object,
        _shifts.Object,
        _orders.Object,
        _confirmations.Object,
        _scheduledOps.Object,
        _machines.Object,
        _products.Object,
        _andon.Object,
        _clock.Object,
        _operationNodes.Object,
        _skills.Object,
        _qualifications.Object);

    private static Operator MakeOperator(string identifier) => new()
    {
        Id = Guid.NewGuid(),
        Identifier = identifier,
        FirstName = "Jan",
        LastName = "Kowalski",
        RatePerHour = 10m,
        UserId = Guid.Empty
    };

    private static Shift MakeShift(string code, string start = "06:00", string end = "14:00") => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Name = code,
        StartTime = TimeOnly.Parse(start),
        EndTime = TimeOnly.Parse(end),
        IsActive = true
    };

    private static OperatorShiftAssignment MakeAssignment(Guid operatorId, Guid shiftId, DateOnly date) => new()
    {
        Id = Guid.NewGuid(),
        OperatorId = operatorId,
        ShiftId = shiftId,
        Date = date
    };

    private static ProductionOrder MakeOrder(
        string code,
        int priority = 0,
        DateTime? dueDate = null,
        decimal planned = 100m) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        ProductId = Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        RecipeVersionId = Guid.NewGuid(),
        PlannedQuantity = planned,
        Status = ProductionOrderStatus.Released,
        DueDate = dueDate,
        Priority = priority,
        CreatedAt = new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc)
    };

    private static Machine MakeMachine(string code) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Name = code,
        CreatedAt = new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc)
    };

    private void ArrangeRoster(
        Operator @operator,
        Shift shift,
        DateOnly date,
        DateTime nowUtc)
    {
        _operators.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<Operator>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([@operator]);
        _assignments.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<OperatorShiftAssignment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeAssignment(@operator.Id, shift.Id, date)]);
        _shifts.Setup(r => r.GetByIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([shift]);
        _clock.SetupGet(p => p.UtcNow).Returns(nowUtc);
    }

    private void ArrangeQueueReads(
        IReadOnlyCollection<ProductionOrder>? orders = null,
        IReadOnlyCollection<ScheduledOperation>? overrides = null,
        IReadOnlyCollection<Machine>? machines = null,
        IReadOnlyCollection<AndonSignal>? signals = null)
    {
        _orders.Setup(r => r.BrowseDispatchBoardAsync(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(orders?.ToList() ?? []);
        _confirmations.Setup(r => r.GetTotalsForOrdersAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, (decimal, decimal, int)>());
        _scheduledOps.Setup(r => r.ListForOrdersAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(overrides?.ToList() ?? []);
        _machines.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<Machine>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(machines?.ToList() ?? []);
        _products.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<Product>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _andon.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<AndonSignal>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(signals?.ToList() ?? []);
    }

    [Fact]
    public void Request_ImplementsTenantRequest_AndNotAnonymous()
    {
        // Assert - the queue query must stay tenant-scoped
        typeof(ITenantRequest<OperatorShiftQueueResponse>)
            .IsAssignableFrom(typeof(GetOperatorShiftQueueRequest)).Should().BeTrue();
        typeof(IAllowAnonymousRequest).IsAssignableFrom(typeof(GetOperatorShiftQueueRequest)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_EmptyOperatorCode_ThrowsValidationException_WithoutReads()
    {
        // Act
        var act = () => CreateSut().Handle(new GetOperatorShiftQueueRequest(""), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _operators.Verify(
            r => r.BrowseAsync(It.IsAny<Paginator<Operator>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_NonPositiveTake_ThrowsValidationException_WithoutReads()
    {
        // Act
        var act = () => CreateSut().Handle(new GetOperatorShiftQueueRequest("OP-1", 0), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _operators.Verify(
            r => r.BrowseAsync(It.IsAny<Paginator<Operator>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownOperator_ThrowsNotFoundException()
    {
        // Arrange
        _operators.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<Operator>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var act = () => CreateSut().Handle(new GetOperatorShiftQueueRequest("OP-NOPE"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _orders.Verify(
            r => r.BrowseDispatchBoardAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_NoAssignments_ReturnsEmptyQueue_WithOperatorContext()
    {
        // Arrange
        var @operator = MakeOperator("OP-1");
        _operators.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<Operator>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([@operator]);
        _assignments.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<OperatorShiftAssignment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await CreateSut().Handle(new GetOperatorShiftQueueRequest("OP-1"), CancellationToken.None);

        // Assert - 200 with an empty queue plus operator context, not 404
        result.OperatorCode.Should().Be("OP-1");
        result.OperatorId.Should().Be(@operator.Id);
        result.Shift.Should().BeNull();
        result.Orders.Should().BeEmpty();
        result.ActiveSignals.Should().BeEmpty();
        _orders.Verify(
            r => r.BrowseDispatchBoardAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_AssignmentNotCoveringNow_ReturnsEmptyQueue_WithoutOrderRead()
    {
        // Arrange - rostered yesterday, but nothing covers now
        var @operator = MakeOperator("OP-1");
        var shift = MakeShift("AM");
        ArrangeRoster(@operator, shift, ShiftDate.AddDays(-1), NowInShift);

        // Act
        var result = await CreateSut().Handle(new GetOperatorShiftQueueRequest("OP-1"), CancellationToken.None);

        // Assert
        result.OperatorId.Should().Be(@operator.Id);
        result.Shift.Should().BeNull();
        result.Orders.Should().BeEmpty();
        result.ActiveSignals.Should().BeEmpty();
        _orders.Verify(
            r => r.BrowseDispatchBoardAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_CoveringAssignment_ReturnsShiftContext_AndReadsShiftWindow()
    {
        // Arrange
        var @operator = MakeOperator("OP-1");
        var shift = MakeShift("AM");
        ArrangeRoster(@operator, shift, ShiftDate, NowInShift);
        ArrangeQueueReads();

        // Act
        var result = await CreateSut().Handle(new GetOperatorShiftQueueRequest("OP-1"), CancellationToken.None);

        // Assert
        result.Shift.Should().NotBeNull();
        result.Shift!.ShiftId.Should().Be(shift.Id);
        result.Shift.ShiftCode.Should().Be("AM");
        result.Shift.Date.Should().Be(ShiftDate);
        result.Shift.WindowStartUtc.Should().Be(new DateTime(2026, 9, 24, 6, 0, 0, DateTimeKind.Utc));
        result.Shift.WindowEndUtc.Should().Be(new DateTime(2026, 9, 24, 14, 0, 0, DateTimeKind.Utc));
        result.Shift.IsOvernight.Should().BeFalse();
        _orders.Verify(r => r.BrowseDispatchBoardAsync(ShiftDate, ShiftDate, 200, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OrdersAhead_ReturnedByPriorityThenDueDate()
    {
        // Arrange - the bounded read returns candidates; next-up ordering is
        // priority first, then due date with nulls last.
        var @operator = MakeOperator("OP-1");
        var shift = MakeShift("AM");
        ArrangeRoster(@operator, shift, ShiftDate, NowInShift);
        var lowLate = MakeOrder("PO-LOW-LATE", priority: 9, dueDate: new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc));
        var highSoon = MakeOrder("PO-HIGH-SOON", priority: 1, dueDate: new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc));
        var highNoDue = MakeOrder("PO-HIGH-NODUE", priority: 1, dueDate: null);
        ArrangeQueueReads(orders: [lowLate, highNoDue, highSoon]);

        // Act
        var result = await CreateSut().Handle(new GetOperatorShiftQueueRequest("OP-1"), CancellationToken.None);

        // Assert
        result.Orders.Select(o => o.Code).Should().Equal("PO-HIGH-SOON", "PO-HIGH-NODUE", "PO-LOW-LATE");
        result.Orders.Should().OnlyContain(o => o.Status == ProductionOrderStatus.Released);
    }

    [Fact]
    public async Task Handle_OversizedTake_IsClampedTo200()
    {
        // Arrange
        var @operator = MakeOperator("OP-1");
        var shift = MakeShift("AM");
        ArrangeRoster(@operator, shift, ShiftDate, NowInShift);
        ArrangeQueueReads();

        // Act
        await CreateSut().Handle(new GetOperatorShiftQueueRequest("OP-1", 5000), CancellationToken.None);

        // Assert - server-side Take stays bounded at the dispatch board cap
        _orders.Verify(r => r.BrowseDispatchBoardAsync(ShiftDate, ShiftDate, 200, It.IsAny<CancellationToken>()), Times.Once);
        _orders.Verify(
            r => r.BrowseAsync(It.IsAny<Paginator<ProductionOrder>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_MapsTotalsRemaining_AndMachineAndProductCodes()
    {
        // Arrange
        var @operator = MakeOperator("OP-1");
        var shift = MakeShift("AM");
        ArrangeRoster(@operator, shift, ShiftDate, NowInShift);
        var order = MakeOrder("PO-001", priority: 2, dueDate: new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc), planned: 100m);
        var machine = MakeMachine("WC-1");
        var scheduled = new ScheduledOperation
        {
            Id = Guid.NewGuid(),
            ProductionOrderId = order.Id,
            OperationNodeId = Guid.NewGuid(),
            MachineId = machine.Id,
            PlannedStart = new DateTime(2026, 9, 24, 7, 0, 0, DateTimeKind.Utc),
            PlannedEnd = new DateTime(2026, 9, 24, 9, 0, 0, DateTimeKind.Utc),
            CreatedAt = NowInShift
        };
        var product = new Product
        {
            Id = order.ProductId,
            Code = "PRD-1",
            Name = "Widget"
        };
        ArrangeQueueReads(orders: [order], overrides: [scheduled], machines: [machine]);
        _products.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<Product>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([product]);
        _confirmations.Setup(r => r.GetTotalsForOrdersAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, (decimal, decimal, int)>
            {
                [order.Id] = (60m, 5m, 3)
            });

        // Act
        var result = await CreateSut().Handle(new GetOperatorShiftQueueRequest("OP-1"), CancellationToken.None);

        // Assert
        var row = result.Orders.Should().ContainSingle(o => o.Code == "PO-001").Subject;
        row.ProductId.Should().Be(order.ProductId);
        row.ProductCode.Should().Be("PRD-1");
        row.PlannedQuantity.Should().Be(100m);
        row.ProducedQuantity.Should().Be(60m);
        row.ScrappedQuantity.Should().Be(5m);
        row.RemainingQuantity.Should().Be(40m);
        row.MachineId.Should().Be(machine.Id);
        row.MachineCode.Should().Be("WC-1");
        row.MachineName.Should().Be("WC-1");
        row.Priority.Should().Be(2);
    }

    [Fact]
    public async Task Handle_OrderWithoutOverride_HasNullMachine_AndSignalsStayEmpty()
    {
        // Arrange - unscheduled orders carry a null Work Center and contribute
        // no lane to scope signals to.
        var @operator = MakeOperator("OP-1");
        var shift = MakeShift("AM");
        ArrangeRoster(@operator, shift, ShiftDate, NowInShift);
        var signalMachine = MakeMachine("WC-9");
        ArrangeQueueReads(
            orders: [MakeOrder("PO-001")],
            machines: [signalMachine],
            signals:
            [
                new AndonSignal
                {
                    Id = Guid.NewGuid(),
                    MachineId = signalMachine.Id,
                    Category = AndonSignalCategory.Downtime,
                    Status = AndonSignalStatus.Active,
                    RaisedAt = NowInShift,
                    CreatedAt = NowInShift
                }
            ]);

        // Act
        var result = await CreateSut().Handle(new GetOperatorShiftQueueRequest("OP-1"), CancellationToken.None);

        // Assert
        result.Orders.Should().ContainSingle().Which.MachineId.Should().BeNull();
        result.ActiveSignals.Should().BeEmpty();
        _andon.Verify(
            r => r.BrowseAsync(It.IsAny<Paginator<AndonSignal>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_SignalsScopedToQueuedMachines_WithCategorySeverity()
    {
        // Arrange - only Active signals on the queued lanes surface, newest
        // first, with severity derived from the category.
        var @operator = MakeOperator("OP-1");
        var shift = MakeShift("AM");
        ArrangeRoster(@operator, shift, ShiftDate, NowInShift);
        var queuedMachine = MakeMachine("WC-1");
        var otherMachine = MakeMachine("WC-2");
        var order = MakeOrder("PO-001", dueDate: new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc));
        var scheduled = new ScheduledOperation
        {
            Id = Guid.NewGuid(),
            ProductionOrderId = order.Id,
            OperationNodeId = Guid.NewGuid(),
            MachineId = queuedMachine.Id,
            PlannedStart = new DateTime(2026, 9, 24, 7, 0, 0, DateTimeKind.Utc),
            PlannedEnd = new DateTime(2026, 9, 24, 9, 0, 0, DateTimeKind.Utc),
            CreatedAt = NowInShift
        };
        var queuedSignal = new AndonSignal
        {
            Id = Guid.NewGuid(),
            MachineId = queuedMachine.Id,
            Category = AndonSignalCategory.Quality,
            Status = AndonSignalStatus.Active,
            RaisedAt = new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc),
            CreatedAt = NowInShift
        };
        var otherLaneSignal = new AndonSignal
        {
            Id = Guid.NewGuid(),
            MachineId = otherMachine.Id,
            Category = AndonSignalCategory.Downtime,
            Status = AndonSignalStatus.Active,
            RaisedAt = new DateTime(2026, 9, 24, 9, 0, 0, DateTimeKind.Utc),
            CreatedAt = NowInShift
        };
        var resolvedSignal = new AndonSignal
        {
            Id = Guid.NewGuid(),
            MachineId = queuedMachine.Id,
            Category = AndonSignalCategory.Material,
            Status = AndonSignalStatus.Resolved,
            RaisedAt = new DateTime(2026, 9, 24, 9, 30, 0, DateTimeKind.Utc),
            CreatedAt = NowInShift
        };
        ArrangeQueueReads(
            orders: [order],
            overrides: [scheduled],
            machines: [queuedMachine, otherMachine],
            signals: [queuedSignal, otherLaneSignal, resolvedSignal]);

        // Act
        var result = await CreateSut().Handle(new GetOperatorShiftQueueRequest("OP-1"), CancellationToken.None);

        // Assert
        var signal = result.ActiveSignals.Should().ContainSingle().Subject;
        signal.Id.Should().Be(queuedSignal.Id);
        signal.MachineId.Should().Be(queuedMachine.Id);
        signal.MachineCode.Should().Be("WC-1");
        signal.Category.Should().Be(AndonSignalCategory.Quality);
        signal.Severity.Should().Be("Quality");
    }

    [Fact]
    public async Task Handle_OvernightShift_CoversAfterMidnight_AndSpansTwoDates()
    {
        // Arrange - 22:00-06:00 rostered on the 24th covers 02:00 on the 25th
        var @operator = MakeOperator("OP-1");
        var night = MakeShift("NIGHT", start: "22:00", end: "06:00");
        ArrangeRoster(@operator, night, ShiftDate, new DateTime(2026, 9, 25, 2, 0, 0, DateTimeKind.Utc));
        ArrangeQueueReads();

        // Act
        var result = await CreateSut().Handle(new GetOperatorShiftQueueRequest("OP-1"), CancellationToken.None);

        // Assert
        result.Shift.Should().NotBeNull();
        result.Shift!.IsOvernight.Should().BeTrue();
        result.Shift.WindowStartUtc.Should().Be(new DateTime(2026, 9, 24, 22, 0, 0, DateTimeKind.Utc));
        result.Shift.WindowEndUtc.Should().Be(new DateTime(2026, 9, 25, 6, 0, 0, DateTimeKind.Utc));
        _orders.Verify(
            r => r.BrowseDispatchBoardAsync(ShiftDate, ShiftDate.AddDays(1), 200, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_OrderRequiringSkill_WithNoQualifiedCrewMember_IsFlagged()
    {
        // Arrange - the queue owner holds no skills and is the only crew member
        var @operator = MakeOperator("OP-1");
        var shift = MakeShift("AM");
        ArrangeRoster(@operator, shift, ShiftDate, NowInShift);
        var order = MakeOrder("PO-SKILL");
        ArrangeQueueReads(orders: [order]);
        ArrangeSkillGate(order.RecipeVersionId, "WELD — Welding");
        _qualifications.Setup(r => r.ListSkillCodesForOperatorsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyCollection<string>>
            {
                [@operator.Id] = []
            });

        // Act
        var result = await CreateSut().Handle(new GetOperatorShiftQueueRequest("OP-1"), CancellationToken.None);

        // Assert
        result.Orders.Should().ContainSingle()
            .Which.NoQualifiedOperator.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_OrderRequiringSkill_WithQualifiedCrewmate_IsNotFlagged()
    {
        // Arrange - the owner is unqualified but a crewmate on the same shift
        // holds the skill, so the order is covered
        var @operator = MakeOperator("OP-1");
        var crewmate = MakeOperator("OP-2");
        var shift = MakeShift("AM");
        ArrangeRoster(@operator, shift, ShiftDate, NowInShift);
        _assignments.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<OperatorShiftAssignment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeAssignment(@operator.Id, shift.Id, ShiftDate),
                MakeAssignment(crewmate.Id, shift.Id, ShiftDate)
            ]);
        var order = MakeOrder("PO-SKILL");
        ArrangeQueueReads(orders: [order]);
        ArrangeSkillGate(order.RecipeVersionId, "WELD — Welding");
        _qualifications.Setup(r => r.ListSkillCodesForOperatorsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyCollection<string>>
            {
                [@operator.Id] = [],
                [crewmate.Id] = ["WELD"]
            });

        // Act
        var result = await CreateSut().Handle(new GetOperatorShiftQueueRequest("OP-1"), CancellationToken.None);

        // Assert
        result.Orders.Should().ContainSingle()
            .Which.NoQualifiedOperator.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_OrderWithoutSkillRequirement_IsNotFlagged()
    {
        // Arrange
        var @operator = MakeOperator("OP-1");
        var shift = MakeShift("AM");
        ArrangeRoster(@operator, shift, ShiftDate, NowInShift);
        ArrangeQueueReads(orders: [MakeOrder("PO-PLAIN")]);

        // Act
        var result = await CreateSut().Handle(new GetOperatorShiftQueueRequest("OP-1"), CancellationToken.None);

        // Assert
        result.Orders.Should().ContainSingle()
            .Which.NoQualifiedOperator.Should().BeFalse();
    }

    private void ArrangeSkillGate(Guid recipeVersionId, string requiredCapability)
    {
        var operation = new OperationNode
        {
            Id = Guid.NewGuid(),
            RecipeVersionId = recipeVersionId,
            Code = "OP-10",
            Name = "Welding",
            ResourceRequirements = new List<ResourceRequirement>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    OperationNodeId = Guid.NewGuid(),
                    RequiredCapability = requiredCapability
                }
            }
        };
        _operationNodes.Setup(r => r.ListForVersionsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperationNode> { operation });
        _skills.Setup(r => r.ListByCodesAsync(
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Skill>
            {
                new() { Id = Guid.NewGuid(), Code = "WELD", Name = "Welding" }
            });
    }
}

public class GetOperatorShiftQueueValidatorTests
{
    private readonly GetOperatorShiftQueueValidator _validator = new();

    [Fact]
    public async Task Validate_EmptyCode_IsInvalid()
    {
        var result = await _validator.ValidateAsync(new GetOperatorShiftQueueRequest(""));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ZeroTake_IsInvalid()
    {
        var result = await _validator.ValidateAsync(new GetOperatorShiftQueueRequest("OP-1", 0));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_OversizedTake_IsValid_BecauseHandlerClampsIt()
    {
        var result = await _validator.ValidateAsync(new GetOperatorShiftQueueRequest("OP-1", 5000));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_CodeAndTake_IsValid()
    {
        var result = await _validator.ValidateAsync(new GetOperatorShiftQueueRequest("OP-1", 50));

        result.IsValid.Should().BeTrue();
    }
}
