using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Production.Application.Features.Schedule;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

/// <summary>
/// Board rendering for held orders (issue #398): the dispatch board and the
/// Gantt keep OnHold rows visible but flagged blocked (never schedulable),
/// while the operator shift queue filters them out entirely.
/// </summary>
public class ProductionOrderHoldBoardsTests
{
    private static readonly DateOnly From = new(2026, 9, 21);
    private static readonly DateOnly To = new(2026, 9, 27);
    private static readonly DateOnly ShiftDate = new(2026, 9, 24);
    private static readonly DateTime NowInShift = new(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc);

    private static ProductionOrder MakeOrder(string code, ProductionOrderStatus status) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        ProductId = Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        RecipeVersionId = Guid.NewGuid(),
        PlannedQuantity = 60m,
        Status = status,
        StatusBeforeHold = status == ProductionOrderStatus.OnHold ? ProductionOrderStatus.Released : null,
        CreatedAt = new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task Handle_DispatchBoard_MarksHeldRowBlocked_AndOpenRowSchedulable()
    {
        var held = MakeOrder("PO-HELD", ProductionOrderStatus.OnHold);
        var released = MakeOrder("PO-OPEN", ProductionOrderStatus.Released);
        var orders = new Mock<IProductionOrdersRepository>();
        orders.Setup(r => r.BrowseDispatchBoardAsync(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([held, released]);
        var confirmations = new Mock<IProductionConfirmationsRepository>();
        confirmations.Setup(r => r.GetTotalsForOrdersAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, (decimal, decimal, int)>());
        var shifts = new Mock<IShiftsRepository>();
        shifts.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<Shift>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var roster = new Mock<IOperatorShiftAssignmentsRepository>();
        roster.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<OperatorShiftAssignment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        // Skill gating (issue #397): no requirements by default so the
        // hold flag is exercised in isolation.
        var operationNodes = new Mock<IOperationNodesRepository>();
        operationNodes.Setup(r => r.ListForVersionsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperationNode>());
        var skills = new Mock<ISkillsRepository>();
        skills.Setup(r => r.ListByCodesAsync(
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Skill>());
        var qualifications = new Mock<IOperatorSkillQualificationsRepository>();
        qualifications.Setup(r => r.ListSkillCodesForOperatorsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyCollection<string>>());
        var sut = new GetDispatchBoardRequestHandler(
            orders.Object, confirmations.Object, shifts.Object, roster.Object,
            operationNodes.Object, skills.Object, qualifications.Object);

        var result = await sut.Handle(new GetDispatchBoardRequest(From, To), CancellationToken.None);

        var heldRow = result.Orders.Single(o => o.Code == "PO-HELD");
        heldRow.Status.Should().Be(ProductionOrderStatus.OnHold);
        heldRow.IsBlocked.Should().BeTrue();
        var openRow = result.Orders.Single(o => o.Code == "PO-OPEN");
        openRow.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_OperatorQueue_ExcludesHeldOrders()
    {
        var held = MakeOrder("PO-HELD", ProductionOrderStatus.OnHold);
        var released = MakeOrder("PO-OPEN", ProductionOrderStatus.Released);
        var @operator = new Operator
        {
            Id = Guid.NewGuid(),
            Identifier = "OP-1",
            FirstName = "Jan",
            LastName = "Kowalski",
            RatePerHour = 10m,
            UserId = Guid.Empty
        };
        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            Code = "AM",
            Name = "AM",
            StartTime = new TimeOnly(6, 0),
            EndTime = new TimeOnly(14, 0),
            IsActive = true
        };
        var operators = new Mock<IOperatorsRepository>();
        operators.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<Operator>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([@operator]);
        var assignments = new Mock<IOperatorShiftAssignmentsRepository>();
        assignments.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<OperatorShiftAssignment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OperatorShiftAssignment
            {
                Id = Guid.NewGuid(),
                OperatorId = @operator.Id,
                ShiftId = shift.Id,
                Date = ShiftDate
            }]);
        var shifts = new Mock<IShiftsRepository>();
        shifts.Setup(r => r.GetByIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([shift]);
        var orders = new Mock<IProductionOrdersRepository>();
        orders.Setup(r => r.BrowseDispatchBoardAsync(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([held, released]);
        var confirmations = new Mock<IProductionConfirmationsRepository>();
        confirmations.Setup(r => r.GetTotalsForOrdersAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, (decimal, decimal, int)>());
        var scheduledOps = new Mock<IScheduledOperationsRepository>();
        scheduledOps.Setup(r => r.ListForOrdersAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var machines = new Mock<IMachinesRepository>();
        machines.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<Machine>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var products = new Mock<IProductsRepository>();
        products.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<Product>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var andon = new Mock<IAndonSignalsRepository>();
        andon.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<AndonSignal>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(p => p.UtcNow).Returns(NowInShift);
        // Skill gating (issue #397): no requirements by default so the
        // held-order exclusion is exercised in isolation.
        var operationNodes = new Mock<IOperationNodesRepository>();
        operationNodes.Setup(r => r.ListForVersionsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperationNode>());
        var skills = new Mock<ISkillsRepository>();
        skills.Setup(r => r.ListByCodesAsync(
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Skill>());
        var qualifications = new Mock<IOperatorSkillQualificationsRepository>();
        qualifications.Setup(r => r.ListSkillCodesForOperatorsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyCollection<string>>());
        var sut = new GetOperatorShiftQueueRequestHandler(
            operators.Object, assignments.Object, shifts.Object, orders.Object,
            confirmations.Object, scheduledOps.Object, machines.Object,
            products.Object, andon.Object, clock.Object,
            operationNodes.Object, skills.Object, qualifications.Object);

        var result = await sut.Handle(new GetOperatorShiftQueueRequest("OP-1"), CancellationToken.None);

        result.Orders.Select(o => o.Code).Should().Equal("PO-OPEN");
        result.Orders.Should().NotContain(o => o.Status == ProductionOrderStatus.OnHold);
    }

    [Fact]
    public async Task Handle_GanttSchedule_MarksHeldOrderBarsBlocked()
    {
        var held = MakeOrder("PO-HELD", ProductionOrderStatus.OnHold);
        var node = new OperationNode
        {
            Id = Guid.NewGuid(),
            RecipeVersionId = held.RecipeVersionId,
            Code = "OP-A",
            Name = "OP-A",
            SortIndex = 0,
            RunTimeMode = RunTimeMode.PerUnitSeconds,
            RunTimePerUnitSeconds = 60m,
            ResourceRequirements = []
        };
        var orders = new Mock<IProductionOrdersRepository>();
        orders.Setup(r => r.BrowseDispatchBoardAsync(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([held]);
        var operations = new Mock<IOperationNodesRepository>();
        operations.Setup(r => r.ListForVersionsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([node]);
        var overrides = new Mock<IScheduledOperationsRepository>();
        overrides.Setup(r => r.ListForOrdersAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var machines = new Mock<IMachinesRepository>();
        machines.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<Machine>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 9, 21, 8, 0, 0, DateTimeKind.Utc));
        var sut = new GetGanttScheduleRequestHandler(
            orders.Object, operations.Object, overrides.Object, machines.Object, clock.Object);

        var result = await sut.Handle(new GetGanttScheduleRequest(From, To, null), CancellationToken.None);

        var bar = result.Groups.SelectMany(g => g.Bars).Should().ContainSingle().Subject;
        bar.ProductionOrderCode.Should().Be("PO-HELD");
        bar.IsBlocked.Should().BeTrue();
    }
}
