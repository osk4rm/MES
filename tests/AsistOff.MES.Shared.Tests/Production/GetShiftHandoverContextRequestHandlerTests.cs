using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.ShiftHandovers;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using FluentAssertions;
using LinqKit;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class GetShiftHandoverContextRequestHandlerTests
{
    private static readonly DateTime From = new(2026, 9, 21, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 9, 21, 14, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IMachinesRepository> _machines = new();
    private readonly Mock<IWorkCenterCalendarsRepository> _calendars = new();
    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private readonly Mock<IAndonSignalsRepository> _signals = new();
    private readonly Mock<IProductionConfirmationsRepository> _confirmations = new();
    private readonly Mock<IProductsRepository> _products = new();
    private readonly Mock<IReasonCodesRepository> _reasons = new();
    private readonly Mock<IOperatorsRepository> _operators = new();

    private GetShiftHandoverContextRequestHandler CreateSut() => new(
        _machines.Object,
        _calendars.Object,
        _orders.Object,
        _signals.Object,
        _confirmations.Object,
        _products.Object,
        _reasons.Object,
        _operators.Object);

    private static ProductionOrder MakeOrder(
        string code,
        ProductionOrderStatus status = ProductionOrderStatus.Released,
        int priority = 0,
        DateTime? dueDate = null,
        decimal planned = 100m,
        Guid? productId = null) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        ProductId = productId ?? Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        RecipeVersionId = Guid.NewGuid(),
        PlannedQuantity = planned,
        Status = status,
        Priority = priority,
        DueDate = dueDate,
        CreatedAt = new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc)
    };

    private static AndonSignal MakeSignal(
        Guid machineId,
        AndonSignalStatus status = AndonSignalStatus.Active,
        AndonSignalCategory category = AndonSignalCategory.Downtime,
        DateTime? raisedAt = null,
        Guid? reasonCodeId = null) => new()
    {
        Id = Guid.NewGuid(),
        MachineId = machineId,
        Category = category,
        ReasonCodeId = reasonCodeId,
        Status = status,
        RaisedAt = raisedAt ?? new DateTime(2026, 9, 21, 9, 0, 0, DateTimeKind.Utc),
        CreatedAt = new DateTime(2026, 9, 21, 9, 0, 0, DateTimeKind.Utc)
    };

    private static ProductionConfirmation MakeConfirmation(
        Guid machineId,
        DateTime reportedAt,
        decimal good = 10m,
        decimal scrap = 1m,
        Guid? operatorId = null) => new()
    {
        Id = Guid.NewGuid(),
        ProductionOrderId = Guid.NewGuid(),
        MachineId = machineId,
        ReportedByOperatorId = operatorId,
        ReportedAt = reportedAt,
        GoodQuantity = good,
        ScrapQuantity = scrap,
        CreatedAt = reportedAt
    };

    private static Machine MakeMachine() => new()
    {
        Id = Guid.NewGuid(),
        Code = "WC-1",
        Name = "Work Center 1",
        CreatedAt = From
    };

    private static WorkCenterCalendar MakeCalendar(
        Guid machineId, params WorkCenterCalendarEntry[] entries) => new()
    {
        Id = Guid.NewGuid(),
        MachineId = machineId,
        Entries = entries.ToList()
    };

    private static WorkCenterCalendarEntry MakeEntry(
        DayOfWeek day, string start, string end, Guid? shiftId = null) => new()
    {
        Id = Guid.NewGuid(),
        WorkCenterCalendarId = Guid.NewGuid(),
        DayOfWeek = day,
        StartTime = TimeOnly.Parse(start),
        EndTime = TimeOnly.Parse(end),
        ShiftId = shiftId,
        IsWorking = true
    };

    private void ArrangeEmpty()
    {
        // Mocked repositories ignore predicates; the handler re-filters in
        // memory, so empty reads model an empty tenant scope.
        _orders.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<ProductionOrder>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _confirmations.Setup(r => r.GetTotalsForOrdersAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, (decimal, decimal, int)>());
        _confirmations.Setup(r => r.CountAsync(
                It.IsAny<ExpressionStarter<ProductionConfirmation>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _confirmations.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<ProductionConfirmation>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _signals.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<AndonSignal>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _products.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<Product>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _reasons.Setup(r => r.ListByIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _operators.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<Operator>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    private Machine ArrangeMachineWithCalendar(params WorkCenterCalendarEntry[] entries)
    {
        var machine = MakeMachine();
        _machines.Setup(r => r.GetByIdAsync(machine.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(machine);
        _calendars.Setup(r => r.GetByMachineIdAsync(machine.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeCalendar(machine.Id, entries));
        return machine;
    }

    [Fact]
    public void Request_ImplementsTenantRequest_AndNotAnonymous()
    {
        // Assert - the handover query must stay tenant-scoped
        typeof(ITenantRequest<ShiftHandoverContextResponse>)
            .IsAssignableFrom(typeof(GetShiftHandoverContextRequest)).Should().BeTrue();
        typeof(IAllowAnonymousRequest).IsAssignableFrom(typeof(GetShiftHandoverContextRequest)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_MachineScope_ReturnsOpenOrdersWithTotalsAndProductCodes()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var released = MakeOrder("PO-REL", ProductionOrderStatus.Released, productId: productId);
        var inProgress = MakeOrder("PO-PRG", ProductionOrderStatus.InProgress);
        ArrangeEmpty();
        var machine = ArrangeMachineWithCalendar(
            MakeEntry(DayOfWeek.Monday, "06:00", "14:00", Guid.NewGuid()));
        _orders.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<ProductionOrder>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                released,
                inProgress,
                MakeOrder("PO-PLN", ProductionOrderStatus.Planned),
                MakeOrder("PO-CMP", ProductionOrderStatus.Completed),
                MakeOrder("PO-CLS", ProductionOrderStatus.Closed)
            ]);
        _confirmations.Setup(r => r.GetTotalsForOrdersAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, (decimal, decimal, int)>
            {
                [released.Id] = (60m, 5m, 3)
            });
        _products.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<Product>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Product { Id = productId, Code = "PRD-1", Name = "Widget" }]);

        // Act
        var result = await CreateSut().Handle(
            new GetShiftHandoverContextRequest(machine.Id, From, To), CancellationToken.None);

        // Assert - exactly the open orders with code, product, quantities, priority, due date
        result.OpenOrders.Select(o => o.Code).Should().Equal("PO-PRG", "PO-REL");
        var row = result.OpenOrders.Single(o => o.Code == "PO-REL");
        row.ProductId.Should().Be(productId);
        row.ProductCode.Should().Be("PRD-1");
        row.PlannedQuantity.Should().Be(100m);
        row.ProducedQuantity.Should().Be(60m);
        row.ScrappedQuantity.Should().Be(5m);
        row.Status.Should().Be(ProductionOrderStatus.Released);
        result.OpenOrders.Single(o => o.Code == "PO-PRG").ProductCode.Should().BeNull();
        result.MachineId.Should().Be(machine.Id);
        result.From.Should().Be(From);
        result.To.Should().Be(To);
    }

    [Fact]
    public async Task Handle_ActiveSignalsOnly_AcknowledgedResolvedAndForeignExcluded()
    {
        // Arrange
        ArrangeEmpty();
        var machine = ArrangeMachineWithCalendar();
        var otherMachine = Guid.NewGuid();
        var active = MakeSignal(machine.Id);
        _signals.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<AndonSignal>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                active,
                MakeSignal(machine.Id, AndonSignalStatus.Acknowledged),
                MakeSignal(machine.Id, AndonSignalStatus.Resolved),
                MakeSignal(otherMachine)
            ]);

        // Act
        var result = await CreateSut().Handle(
            new GetShiftHandoverContextRequest(machine.Id, From, To), CancellationToken.None);

        // Assert
        result.ActiveSignals.Should().ContainSingle().Which.Id.Should().Be(active.Id);
        var item = result.ActiveSignals.Single();
        item.MachineId.Should().Be(machine.Id);
        item.RaisedAt.Should().Be(active.RaisedAt);
        item.Severity.Should().Be(nameof(AndonSignalCategory.Downtime));
    }

    [Fact]
    public async Task Handle_SignalCode_UsesReasonCodeOrFallsBackToCategory()
    {
        // Arrange
        ArrangeEmpty();
        var machine = ArrangeMachineWithCalendar();
        var reasonId = Guid.NewGuid();
        _signals.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<AndonSignal>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeSignal(machine.Id, category: AndonSignalCategory.Quality, reasonCodeId: reasonId),
                MakeSignal(machine.Id, category: AndonSignalCategory.Material)
            ]);
        _reasons.Setup(r => r.ListByIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ReasonCode { Id = reasonId, Code = "QLT-SCRATCH", Name = "Scratch" }]);

        // Act
        var result = await CreateSut().Handle(
            new GetShiftHandoverContextRequest(machine.Id, From, To), CancellationToken.None);

        // Assert
        result.ActiveSignals.Should().HaveCount(2);
        result.ActiveSignals.Single(s => s.Category == AndonSignalCategory.Quality).Code.Should().Be("QLT-SCRATCH");
        result.ActiveSignals.Single(s => s.Category == AndonSignalCategory.Material).Code.Should().Be(nameof(AndonSignalCategory.Material));
    }

    [Fact]
    public async Task Handle_Confirmations_NewestFirst_InsideWindowOnlyWithOperatorCode()
    {
        // Arrange
        ArrangeEmpty();
        var machine = ArrangeMachineWithCalendar();
        var operatorId = Guid.NewGuid();
        var older = MakeConfirmation(machine.Id, new DateTime(2026, 9, 21, 9, 0, 0, DateTimeKind.Utc), operatorId: operatorId);
        var newer = MakeConfirmation(machine.Id, new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc));
        _confirmations.Setup(r => r.CountAsync(
                It.IsAny<ExpressionStarter<ProductionConfirmation>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _confirmations.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<ProductionConfirmation>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                older,
                newer,
                // Outside the window and another Work Center: excluded.
                MakeConfirmation(machine.Id, new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc)),
                MakeConfirmation(machine.Id, new DateTime(2026, 9, 21, 15, 0, 0, DateTimeKind.Utc)),
                MakeConfirmation(Guid.NewGuid(), new DateTime(2026, 9, 21, 10, 0, 0, DateTimeKind.Utc))
            ]);
        _operators.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<Operator>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Operator
            {
                Id = operatorId, Identifier = "OP-7", FirstName = "Jan", LastName = "K", RatePerHour = 10m
            }]);

        // Act
        var result = await CreateSut().Handle(
            new GetShiftHandoverContextRequest(machine.Id, From, To), CancellationToken.None);

        // Assert - newest first with produced/scrapped, Work Center, operator code
        result.Confirmations.Select(c => c.Id).Should().Equal(newer.Id, older.Id);
        result.ConfirmationsTotalCount.Should().Be(2);
        result.Confirmations.Should().OnlyContain(c => c.MachineId == machine.Id);
        result.Confirmations.Single(c => c.Id == older.Id).OperatorCode.Should().Be("OP-7");
        result.Confirmations.Single(c => c.Id == newer.Id).OperatorCode.Should().BeNull();
        result.Confirmations.Single(c => c.Id == older.Id).GoodQuantity.Should().Be(10m);
        result.Confirmations.Single(c => c.Id == older.Id).ScrapQuantity.Should().Be(1m);
    }

    [Fact]
    public async Task Handle_Confirmations_DefaultPage20_AndHonoursRequestedPage()
    {
        // Arrange
        ArrangeEmpty();
        var machine = ArrangeMachineWithCalendar();
        var rows = Enumerable.Range(0, 25)
            .Select(i => MakeConfirmation(machine.Id, To.AddMinutes(-i - 1)))
            .ToList();
        _confirmations.Setup(r => r.CountAsync(
                It.IsAny<ExpressionStarter<ProductionConfirmation>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(25);
        // The repository applies paging in production, so the mock simulates
        // it: slice the pre-sorted rows by the requested page/size instead of
        // returning everything (the handler must NOT Skip/Take a second time).
        _confirmations.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<ProductionConfirmation>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Paginator<ProductionConfirmation> paginator, CancellationToken _) =>
            {
                var pageNumber = paginator.Paging.PageNumber ?? 1;
                var size = paginator.Paging.PageSize ?? rows.Count;
                return rows
                    .OrderByDescending(x => x.ReportedAt)
                    .ThenBy(x => x.Id)
                    .Skip((pageNumber - 1) * size)
                    .Take(size)
                    .ToList();
            });

        // Act - default page size 20
        var @default = await CreateSut().Handle(
            new GetShiftHandoverContextRequest(machine.Id, From, To), CancellationToken.None);

        // Assert
        @default.Confirmations.Should().HaveCount(20);
        @default.Confirmations.Should().BeInDescendingOrder(c => c.ReportedAt);
        @default.ConfirmationPage.Should().Be(1);
        @default.ConfirmationPageSize.Should().Be(20);
        @default.ConfirmationsTotalCount.Should().Be(25);

        // Act - second page of 10
        var second = await CreateSut().Handle(
            new GetShiftHandoverContextRequest(machine.Id, From, To, 2, 10), CancellationToken.None);

        // Assert
        second.Confirmations.Should().HaveCount(10);
        second.ConfirmationPage.Should().Be(2);
        second.ConfirmationPageSize.Should().Be(10);
    }

    [Fact]
    public async Task Handle_ShiftMatch_ReturnsShiftId_NotUncovered()
    {
        // Arrange - From is Monday 2026-09-21 08:00 UTC inside 06:00-14:00
        ArrangeEmpty();
        var shiftId = Guid.NewGuid();
        var machine = ArrangeMachineWithCalendar(
            MakeEntry(DayOfWeek.Monday, "06:00", "14:00", shiftId));

        // Act
        var result = await CreateSut().Handle(
            new GetShiftHandoverContextRequest(machine.Id, From, To), CancellationToken.None);

        // Assert
        result.ShiftId.Should().Be(shiftId);
        result.UncoveredShift.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShiftGap_ReturnsNullShiftId_WithUncoveredTrue()
    {
        // Arrange - entry covers Tuesday only, From is Monday
        ArrangeEmpty();
        var machine = ArrangeMachineWithCalendar(
            MakeEntry(DayOfWeek.Tuesday, "06:00", "14:00", Guid.NewGuid()));

        // Act
        var result = await CreateSut().Handle(
            new GetShiftHandoverContextRequest(machine.Id, From, To), CancellationToken.None);

        // Assert - no 404: empty shift window with the uncovered flag
        result.ShiftId.Should().BeNull();
        result.UncoveredShift.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MachineWithoutCalendar_ReturnsNullShiftId_WithUncoveredTrue()
    {
        // Arrange
        ArrangeEmpty();
        var machine = MakeMachine();
        _machines.Setup(r => r.GetByIdAsync(machine.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(machine);
        _calendars.Setup(r => r.GetByMachineIdAsync(machine.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkCenterCalendar?)null);

        // Act
        var result = await CreateSut().Handle(
            new GetShiftHandoverContextRequest(machine.Id, From, To), CancellationToken.None);

        // Assert
        result.ShiftId.Should().BeNull();
        result.UncoveredShift.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_OvernightEntry_CoversAfterMidnight()
    {
        // Arrange - Sunday 22:00-06:00 covers Monday 02:00
        ArrangeEmpty();
        var shiftId = Guid.NewGuid();
        var machine = ArrangeMachineWithCalendar(
            MakeEntry(DayOfWeek.Sunday, "22:00", "06:00", shiftId));
        var mondayEarly = new DateTime(2026, 9, 21, 2, 0, 0, DateTimeKind.Utc);
        var mondayMorning = new DateTime(2026, 9, 21, 8, 0, 0, DateTimeKind.Utc);

        // Act
        var result = await CreateSut().Handle(
            new GetShiftHandoverContextRequest(machine.Id, mondayEarly, mondayMorning),
            CancellationToken.None);

        // Assert
        result.ShiftId.Should().Be(shiftId);
        result.UncoveredShift.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_EntryEnd_IsExclusiveBoundary()
    {
        // Arrange - entry 06:00-14:00: 14:00 exactly is outside, 06:00 exactly inside
        ArrangeEmpty();
        var shiftId = Guid.NewGuid();
        var machine = ArrangeMachineWithCalendar(
            MakeEntry(DayOfWeek.Monday, "06:00", "14:00", shiftId));
        var atEnd = new DateTime(2026, 9, 21, 14, 0, 0, DateTimeKind.Utc);
        var atStart = new DateTime(2026, 9, 21, 6, 0, 0, DateTimeKind.Utc);

        // Act
        var outside = await CreateSut().Handle(
            new GetShiftHandoverContextRequest(machine.Id, atEnd, atEnd.AddHours(1)),
            CancellationToken.None);
        var inside = await CreateSut().Handle(
            new GetShiftHandoverContextRequest(machine.Id, atStart, atStart.AddHours(1)),
            CancellationToken.None);

        // Assert
        outside.ShiftId.Should().BeNull();
        outside.UncoveredShift.Should().BeTrue();
        inside.ShiftId.Should().Be(shiftId);
        inside.UncoveredShift.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NoMachineScope_TenantWideWithNullShift_NotUncovered()
    {
        // Arrange
        ArrangeEmpty();
        var machineId = Guid.NewGuid();
        _signals.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<AndonSignal>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeSignal(machineId), MakeSignal(Guid.NewGuid())]);

        // Act
        var result = await CreateSut().Handle(
            new GetShiftHandoverContextRequest(null, From, To), CancellationToken.None);

        // Assert - no single calendar without a machine scope
        result.MachineId.Should().BeNull();
        result.ShiftId.Should().BeNull();
        result.UncoveredShift.Should().BeFalse();
        result.ActiveSignals.Should().HaveCount(2);
        _machines.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _calendars.Verify(r => r.GetByMachineIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EmptyScope_ReturnsEmptyLists_Not404()
    {
        // Arrange
        ArrangeEmpty();
        var machine = ArrangeMachineWithCalendar();

        // Act
        var result = await CreateSut().Handle(
            new GetShiftHandoverContextRequest(machine.Id, From, To), CancellationToken.None);

        // Assert
        result.OpenOrders.Should().BeEmpty();
        result.ActiveSignals.Should().BeEmpty();
        result.Confirmations.Should().BeEmpty();
        result.ConfirmationsTotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_CapsOrdersAndSignals_At100()
    {
        // Arrange
        ArrangeEmpty();
        var machine = ArrangeMachineWithCalendar();
        _orders.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<ProductionOrder>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, 105)
                .Select(i => MakeOrder($"PO-{i:000}"))
                .ToList());
        _signals.Setup(r => r.BrowseAsync(
                It.IsAny<Paginator<AndonSignal>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, 105)
                .Select(i => MakeSignal(Guid.NewGuid()))
                .ToList());

        // Act
        var result = await CreateSut().Handle(
            new GetShiftHandoverContextRequest(null, From, To), CancellationToken.None);

        // Assert - defensive in-memory Take guards repositories that ignore Take
        result.OpenOrders.Should().HaveCount(100);
        result.ActiveSignals.Should().HaveCount(100);
    }

    [Fact]
    public async Task Handle_UnknownMachine_ThrowsNotFoundException()
    {
        // Arrange
        ArrangeEmpty();
        var unknown = Guid.NewGuid();
        _machines.Setup(r => r.GetByIdAsync(unknown, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Machine?)null);

        // Act
        var act = () => CreateSut().Handle(
            new GetShiftHandoverContextRequest(unknown, From, To), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ReversedWindow_ThrowsValidationException_WithoutReads()
    {
        // Arrange
        ArrangeEmpty();

        // Act
        var act = () => CreateSut().Handle(
            new GetShiftHandoverContextRequest(null, To, From), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _orders.Verify(r => r.BrowseAsync(
            It.IsAny<Paginator<ProductionOrder>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EqualBounds_ThrowsValidationException()
    {
        // Arrange
        ArrangeEmpty();

        // Act
        var act = () => CreateSut().Handle(
            new GetShiftHandoverContextRequest(null, From, From), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_WindowOver24Hours_ThrowsValidationException()
    {
        // Arrange
        ArrangeEmpty();

        // Act
        var act = () => CreateSut().Handle(
            new GetShiftHandoverContextRequest(null, From, From.AddHours(25)),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_WindowExactly24Hours_Succeeds()
    {
        // Arrange
        ArrangeEmpty();

        // Act
        var result = await CreateSut().Handle(
            new GetShiftHandoverContextRequest(null, From, From.AddHours(24)),
            CancellationToken.None);

        // Assert
        result.From.Should().Be(From);
        result.To.Should().Be(From.AddHours(24));
    }

    [Fact]
    public async Task Handle_MissingDates_ThrowsValidationException()
    {
        // Arrange
        ArrangeEmpty();

        // Act
        var act = () => CreateSut().Handle(
            new GetShiftHandoverContextRequest(null, default, To), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_EmptyMachineId_ThrowsValidationException()
    {
        // Arrange
        ArrangeEmpty();

        // Act
        var act = () => CreateSut().Handle(
            new GetShiftHandoverContextRequest(Guid.Empty, From, To), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }
}
