using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Schedule;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class GetDispatchBoardRequestHandlerTests
{
    private static readonly DateOnly From = new(2026, 9, 21);
    private static readonly DateOnly To = new(2026, 9, 27);

    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private readonly Mock<IProductionConfirmationsRepository> _confirmations = new();
    private readonly Mock<IShiftsRepository> _shifts = new();
    private readonly Mock<IOperatorShiftAssignmentsRepository> _roster = new();

    private GetDispatchBoardRequestHandler CreateSut() => new(
        _orders.Object, _confirmations.Object, _shifts.Object, _roster.Object);

    private static ProductionOrder MakeOrder(
        string code,
        ProductionOrderStatus status = ProductionOrderStatus.Released,
        DateTime? dueDate = null,
        int priority = 0,
        decimal planned = 100m) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        ProductId = Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        RecipeVersionId = Guid.NewGuid(),
        PlannedQuantity = planned,
        Status = status,
        DueDate = dueDate,
        Priority = priority,
        CreatedAt = new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc)
    };

    private static Shift MakeShift(string code, bool isActive = true, string start = "06:00", string end = "14:00") => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Name = code,
        StartTime = TimeOnly.Parse(start),
        EndTime = TimeOnly.Parse(end),
        IsActive = isActive
    };

    private static OperatorShiftAssignment MakeAssignment(Guid operatorId, Guid shiftId, DateOnly date) => new()
    {
        Id = Guid.NewGuid(),
        OperatorId = operatorId,
        ShiftId = shiftId,
        Date = date
    };

    private void ArrangeEmpty(
        IReadOnlyCollection<ProductionOrder>? orders = null,
        IReadOnlyCollection<Shift>? shifts = null,
        IReadOnlyCollection<OperatorShiftAssignment>? roster = null)
    {
        // The bounded dispatch read owns status, window, ordering and Take
        // server-side; the mock simply returns the rows the database would.
        _orders.Setup(r => r.BrowseDispatchBoardAsync(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(orders?.ToList() ?? []);
        _confirmations.Setup(r => r.GetTotalsForOrdersAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, (decimal, decimal, int)>());
        _shifts.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<Shift>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(shifts?.ToList() ?? []);
        _roster.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<OperatorShiftAssignment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(roster?.ToList() ?? []);
    }

    [Fact]
    public void Request_ImplementsTenantRequest_AndNotAnonymous()
    {
        // Assert - the dispatch query must stay tenant-scoped
        typeof(ITenantRequest<DispatchBoardResponse>)
            .IsAssignableFrom(typeof(GetDispatchBoardRequest)).Should().BeTrue();
        typeof(IAllowAnonymousRequest).IsAssignableFrom(typeof(GetDispatchBoardRequest)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReturnsOneBucketPerDay_WithActiveShiftsOnly()
    {
        // Arrange
        var active = MakeShift("A-MORNING");
        var inactive = MakeShift("Z-NIGHT", isActive: false);
        ArrangeEmpty(orders: [], shifts: [active, inactive], roster: []);

        // Act
        var result = await CreateSut().Handle(new GetDispatchBoardRequest(From, To), CancellationToken.None);

        // Assert
        result.From.Should().Be(From);
        result.To.Should().Be(To);
        result.Days.Should().HaveCount(7);
        result.Days.Select(d => d.Date).Should().BeInAscendingOrder();
        result.Days.First().Date.Should().Be(From);
        result.Days.Last().Date.Should().Be(To);
        result.Days.Should().OnlyContain(d => d.Shifts.Select(s => s.Code).Contains("A-MORNING"));
        result.Days.Should().OnlyContain(d => d.Shifts.All(s => s.Code != "Z-NIGHT"));
    }

    [Fact]
    public async Task Handle_AggregatesHeadcount_PerShiftAndDate()
    {
        // Arrange
        var morning = MakeShift("A-MORNING");
        var night = MakeShift("B-NIGHT", start: "22:00", end: "06:00");
        var opA = Guid.NewGuid();
        var opB = Guid.NewGuid();
        ArrangeEmpty(
            shifts: [morning, night],
            roster:
            [
                MakeAssignment(opA, morning.Id, From),
                MakeAssignment(opB, morning.Id, From),
                MakeAssignment(opA, night.Id, From.AddDays(1)),
                // Outside the window: must not leak into any bucket.
                MakeAssignment(opA, morning.Id, From.AddDays(-1)),
                MakeAssignment(opA, morning.Id, To.AddDays(1))
            ]);

        // Act
        var result = await CreateSut().Handle(new GetDispatchBoardRequest(From, To), CancellationToken.None);

        // Assert
        var firstDay = result.Days.Single(d => d.Date == From);
        firstDay.Shifts.Single(s => s.ShiftId == morning.Id).Headcount.Should().Be(2);
        firstDay.Shifts.Single(s => s.ShiftId == night.Id).Headcount.Should().Be(0);

        var secondDay = result.Days.Single(d => d.Date == From.AddDays(1));
        secondDay.Shifts.Single(s => s.ShiftId == night.Id).Headcount.Should().Be(1);
        secondDay.Shifts.Single(s => s.ShiftId == morning.Id).Headcount.Should().Be(0);

        // Overnight windows cross midnight.
        firstDay.Shifts.Single(s => s.ShiftId == morning.Id).IsOvernight.Should().BeFalse();
        firstDay.Shifts.Single(s => s.ShiftId == night.Id).IsOvernight.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_IssuesBoundedQuery_WithWindowAndTake200()
    {
        // Arrange - the database owns status, window, ordering and Take; the
        // handler must pass the window through with the 200-row bound.
        ArrangeEmpty();

        // Act
        await CreateSut().Handle(new GetDispatchBoardRequest(From, To), CancellationToken.None);

        // Assert
        _orders.Verify(r => r.BrowseDispatchBoardAsync(From, To, 200, It.IsAny<CancellationToken>()), Times.Once);
        _orders.Verify(
            r => r.BrowseAsync(It.IsAny<Paginator<ProductionOrder>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_PreservesRepositoryOrder_AndMapsOverdueFlag()
    {
        // Arrange - filtering and ordering happen server-side, so the handler
        // preserves the repository order and only derives the overdue flag.
        var overdue = MakeOrder("PO-OVERDUE", dueDate: new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc));
        var dueSoon = MakeOrder("PO-DUE-SOON", status: ProductionOrderStatus.InProgress, dueDate: new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc));
        var noDue = MakeOrder("PO-NODUE", dueDate: null);
        ArrangeEmpty(orders: [overdue, dueSoon, noDue]);

        // Act
        var result = await CreateSut().Handle(new GetDispatchBoardRequest(From, To), CancellationToken.None);

        // Assert - repository order preserved, overdue derived from the window start.
        result.Orders.Select(o => o.Code).Should().Equal("PO-OVERDUE", "PO-DUE-SOON", "PO-NODUE");
        result.Orders.Single(o => o.Code == "PO-OVERDUE").IsOverdue.Should().BeTrue();
        result.Orders.Single(o => o.Code == "PO-DUE-SOON").IsOverdue.Should().BeFalse();
        result.Orders.Single(o => o.Code == "PO-NODUE").IsOverdue.Should().BeFalse();
        result.Orders.Single(o => o.Code == "PO-DUE-SOON").Status.Should().Be(ProductionOrderStatus.InProgress);
    }

    [Fact]
    public async Task Handle_EmptyWindow_ReturnsEmptyBoard_WithShiftsIntact()
    {
        // Arrange
        var morning = MakeShift("A-MORNING");
        ArrangeEmpty(orders: [], shifts: [morning], roster: []);

        // Act
        var result = await CreateSut().Handle(new GetDispatchBoardRequest(From, To), CancellationToken.None);

        // Assert
        result.Orders.Should().BeEmpty();
        result.Days.Should().HaveCount(7);
        result.Days.Should().OnlyContain(d => d.Shifts.Select(s => s.Code).Contains("A-MORNING"));
    }

    [Fact]
    public async Task Handle_LoadsConfirmationTotals_InSingleBatchedCall()
    {
        // Arrange
        var first = MakeOrder("PO-001", dueDate: new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc));
        var second = MakeOrder("PO-002", dueDate: new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc));
        ArrangeEmpty(orders: [first, second]);

        // Act
        await CreateSut().Handle(new GetDispatchBoardRequest(From, To), CancellationToken.None);

        // Assert - one grouped query for all shown orders, no per-order query.
        _confirmations.Verify(
            r => r.GetTotalsForOrdersAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2 && ids.Contains(first.Id) && ids.Contains(second.Id)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_MapsConfirmationTotals_AndRemaining()
    {
        // Arrange
        var order = MakeOrder("PO-001", dueDate: new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc), planned: 100m);
        ArrangeEmpty(orders: [order]);
        _confirmations.Setup(r => r.GetTotalsForOrdersAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, (decimal, decimal, int)>
            {
                [order.Id] = (60m, 5m, 3)
            });

        // Act
        var result = await CreateSut().Handle(new GetDispatchBoardRequest(From, To), CancellationToken.None);

        // Assert
        var row = result.Orders.Should().ContainSingle(o => o.Code == "PO-001").Subject;
        row.ProducedQuantity.Should().Be(60m);
        row.ScrappedQuantity.Should().Be(5m);
        row.RemainingQuantity.Should().Be(40m);
        row.Priority.Should().Be(order.Priority);
        row.Status.Should().Be(ProductionOrderStatus.Released);
        row.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_CapsOrderRows_At200()
    {
        // Arrange - defensive in-memory Take guards repositories that ignore
        // the Take; the database applies it first.
        var orders = Enumerable.Range(0, 205)
            .Select(i => MakeOrder($"PO-{i:000}", dueDate: null))
            .ToList();
        ArrangeEmpty(orders: orders);

        // Act
        var result = await CreateSut().Handle(new GetDispatchBoardRequest(From, To), CancellationToken.None);

        // Assert
        result.Orders.Should().HaveCount(200);
    }

    [Fact]
    public async Task Handle_ReversedWindow_ThrowsValidationException_WithoutOrderRead()
    {
        // Arrange
        ArrangeEmpty();

        // Act
        var act = () => CreateSut().Handle(new GetDispatchBoardRequest(To, From), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _orders.Verify(
            r => r.BrowseDispatchBoardAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_MissingDates_ThrowsValidationException_WithoutOrderRead()
    {
        // Arrange
        ArrangeEmpty();

        // Act
        var act = () => CreateSut().Handle(new GetDispatchBoardRequest(default, To), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _orders.Verify(
            r => r.BrowseDispatchBoardAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_Window32Days_ThrowsValidationException_WithoutOrderRead()
    {
        // Arrange
        ArrangeEmpty();

        // Act
        var act = () => CreateSut().Handle(
            new GetDispatchBoardRequest(new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 2)),
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
        ArrangeEmpty();

        // Act
        var result = await CreateSut().Handle(
            new GetDispatchBoardRequest(new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1)),
            CancellationToken.None);

        // Assert
        result.Days.Should().HaveCount(31);
    }

    [Fact]
    public async Task Handle_SingleDayWindow_Succeeds()
    {
        // Arrange
        ArrangeEmpty();

        // Act
        var result = await CreateSut().Handle(new GetDispatchBoardRequest(From, From), CancellationToken.None);

        // Assert
        result.Days.Should().ContainSingle().Which.Date.Should().Be(From);
    }

    [Fact]
    public async Task Handle_ShiftWithoutAssignments_IsUncovered()
    {
        // Arrange
        var morning = MakeShift("A-MORNING");
        ArrangeEmpty(shifts: [morning], roster: []);

        // Act
        var result = await CreateSut().Handle(new GetDispatchBoardRequest(From, To), CancellationToken.None);

        // Assert
        result.Days.Should().OnlyContain(d => d.Shifts.Single(s => s.ShiftId == morning.Id).Headcount == 0);
        result.Days.Should().OnlyContain(d => d.Shifts.Single(s => s.ShiftId == morning.Id).IsUncovered);
    }

    [Fact]
    public async Task Handle_ShiftWithAssignment_IsNotUncovered()
    {
        // Arrange
        var morning = MakeShift("A-MORNING");
        var opA = Guid.NewGuid();
        ArrangeEmpty(
            shifts: [morning],
            roster: [MakeAssignment(opA, morning.Id, From)]);

        // Act
        var result = await CreateSut().Handle(new GetDispatchBoardRequest(From, To), CancellationToken.None);

        // Assert
        var coveredDay = result.Days.Single(d => d.Date == From);
        coveredDay.Shifts.Single(s => s.ShiftId == morning.Id).Headcount.Should().Be(1);
        coveredDay.Shifts.Single(s => s.ShiftId == morning.Id).IsUncovered.Should().BeFalse();

        result.Days.Where(d => d.Date != From).Should().OnlyContain(
            d => d.Shifts.Single(s => s.ShiftId == morning.Id).IsUncovered);
    }

    [Fact]
    public async Task Handle_OvernightShift_UncoveredFollowsHeadcount()
    {
        // Arrange
        var night = MakeShift("B-NIGHT", start: "22:00", end: "06:00");
        var opA = Guid.NewGuid();
        ArrangeEmpty(
            shifts: [night],
            roster: [MakeAssignment(opA, night.Id, From.AddDays(1))]);

        // Act
        var result = await CreateSut().Handle(new GetDispatchBoardRequest(From, To), CancellationToken.None);

        // Assert - overnight detection is orthogonal to the uncovered flag.
        var emptyDay = result.Days.Single(d => d.Date == From);
        var emptyShift = emptyDay.Shifts.Single(s => s.ShiftId == night.Id);
        emptyShift.IsOvernight.Should().BeTrue();
        emptyShift.Headcount.Should().Be(0);
        emptyShift.IsUncovered.Should().BeTrue();

        var coveredDay = result.Days.Single(d => d.Date == From.AddDays(1));
        var coveredShift = coveredDay.Shifts.Single(s => s.ShiftId == night.Id);
        coveredShift.IsOvernight.Should().BeTrue();
        coveredShift.Headcount.Should().Be(1);
        coveredShift.IsUncovered.Should().BeFalse();
    }
}
