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
        _orders.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<ProductionOrder>>(), It.IsAny<CancellationToken>()))
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
    public async Task Handle_OrdersOverdueFirst_ThenDueDate_NullsLast_ThenPriority_ThenCode()
    {
        // Arrange - from = 2026-09-21
        var overdueLow = MakeOrder("PO-OVERDUE-B", dueDate: new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc), priority: 5);
        var overdueHigh = MakeOrder("PO-OVERDUE-A", dueDate: new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc), priority: 1);
        var dueLater = MakeOrder("PO-DUE-LATER", dueDate: new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc), priority: 0);
        var dueSoon = MakeOrder("PO-DUE-SOON", dueDate: new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc), priority: 9);
        var noDueLow = MakeOrder("PO-NODUE-B", dueDate: null, priority: 0);
        var noDueHigh = MakeOrder("PO-NODUE-A", dueDate: null, priority: 0);
        var tieLow = MakeOrder("PO-TIE-B", dueDate: new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc), priority: 2);
        var tieHigh = MakeOrder("PO-TIE-A", dueDate: new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc), priority: 1);
        // Excluded: wrong status or due after the window.
        var planned = MakeOrder("PO-PLANNED", ProductionOrderStatus.Planned, new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc));
        var completed = MakeOrder("PO-COMPLETED", ProductionOrderStatus.Completed, new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc));
        var closed = MakeOrder("PO-CLOSED", ProductionOrderStatus.Closed, new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc));
        var afterWindow = MakeOrder("PO-AFTER", ProductionOrderStatus.Released, new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc));
        var inProgress = MakeOrder("PO-INPROG", ProductionOrderStatus.InProgress, new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc));
        ArrangeEmpty(orders:
        [
            dueLater, planned, noDueLow, overdueLow, tieLow, completed,
            dueSoon, closed, noDueHigh, afterWindow, overdueHigh, tieHigh, inProgress
        ]);

        // Act
        var result = await CreateSut().Handle(new GetDispatchBoardRequest(From, To), CancellationToken.None);

        // Assert - overdue first (earlier due date first), then due-date
        // ascending, priority tiebreak, code tiebreak, null due dates last.
        var codes = result.Orders.Select(o => o.Code).ToList();
        codes.Should().Equal(
            "PO-OVERDUE-B", "PO-OVERDUE-A",
            "PO-DUE-SOON", "PO-TIE-A", "PO-TIE-B", "PO-INPROG", "PO-DUE-LATER",
            "PO-NODUE-A", "PO-NODUE-B");
        result.Orders.Take(2).Should().OnlyContain(o => o.IsOverdue);
        result.Orders.Skip(2).Should().OnlyContain(o => !o.IsOverdue);
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
        // Arrange
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
    public async Task Handle_ReversedWindow_ThrowsValidationException()
    {
        // Arrange
        ArrangeEmpty();

        // Act
        var act = () => CreateSut().Handle(new GetDispatchBoardRequest(To, From), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_MissingDates_ThrowsValidationException()
    {
        // Arrange
        ArrangeEmpty();

        // Act
        var act = () => CreateSut().Handle(new GetDispatchBoardRequest(default, To), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_Window32Days_ThrowsValidationException()
    {
        // Arrange
        ArrangeEmpty();

        // Act
        var act = () => CreateSut().Handle(
            new GetDispatchBoardRequest(new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 2)),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
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
}
