using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Production.Application.Features.Schedule;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

/// <summary>
/// Hardening tests for issue #274: the dispatch board must issue a bounded
/// database query (status + window + ordering + Take 200), reject over-wide
/// windows without any database read, keep day buckets on empty windows, and
/// load confirmation totals in one batched call.
/// </summary>
public class DispatchBoardHardeningTests
{
    private static readonly DateOnly From = new(2026, 9, 21);
    private static readonly DateOnly To = new(2026, 9, 27);

    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private readonly Mock<IProductionConfirmationsRepository> _confirmations = new();
    private readonly Mock<IShiftsRepository> _shifts = new();
    private readonly Mock<IOperatorShiftAssignmentsRepository> _roster = new();

    private GetDispatchBoardRequestHandler CreateSut() => new(
        _orders.Object, _confirmations.Object, _shifts.Object, _roster.Object);

    private static ProductionOrder MakeOrder(string code, DateTime? dueDate = null) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        ProductId = Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        RecipeVersionId = Guid.NewGuid(),
        PlannedQuantity = 100m,
        Status = ProductionOrderStatus.Released,
        DueDate = dueDate,
        Priority = 0,
        CreatedAt = new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc)
    };

    private void Arrange(
        IReadOnlyCollection<ProductionOrder>? orders = null,
        IReadOnlyCollection<Shift>? shifts = null,
        IReadOnlyCollection<OperatorShiftAssignment>? roster = null)
    {
        _orders.Setup(r => r.BrowseDispatchAsync(
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
    public async Task Handle_UsesBoundedDispatchQuery_WithWindowAndTake200()
    {
        // Arrange
        Arrange(orders: []);

        // Act
        await CreateSut().Handle(new GetDispatchBoardRequest(From, To), CancellationToken.None);

        // Assert - bounded DB read carries the window plus Take(200).
        _orders.Verify(
            r => r.BrowseDispatchAsync(From, To, 200, It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert - the old unbounded full-table path is gone.
        _orders.Verify(
            r => r.BrowseAsync(It.IsAny<Paginator<ProductionOrder>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_OverWindow_PerformsNoDatabaseRead()
    {
        // Arrange - no setups: any repository call would throw on strict mocks,
        // here we verify zero interactions instead.

        // Act
        var act = () => CreateSut().Handle(
            new GetDispatchBoardRequest(new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 2)),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _orders.Verify(
            r => r.BrowseDispatchAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _orders.Verify(
            r => r.BrowseAsync(It.IsAny<Paginator<ProductionOrder>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _shifts.Verify(
            r => r.BrowseAsync(It.IsAny<Paginator<Shift>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _roster.Verify(
            r => r.BrowseAsync(It.IsAny<Paginator<OperatorShiftAssignment>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _confirmations.Verify(
            r => r.GetTotalsForOrdersAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_EmptyWindow_ReturnsEmptyBoardWithShiftsIntact()
    {
        // Arrange
        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            Code = "A-MORNING",
            Name = "Morning",
            StartTime = TimeOnly.Parse("06:00"),
            EndTime = TimeOnly.Parse("14:00"),
            IsActive = true
        };
        Arrange(orders: [], shifts: [shift], roster: []);

        // Act
        var result = await CreateSut().Handle(new GetDispatchBoardRequest(From, To), CancellationToken.None);

        // Assert
        result.Orders.Should().BeEmpty();
        result.Days.Should().HaveCount(7);
        result.Days.Should().OnlyContain(d => d.Shifts.Count == 1);
        result.Days.Should().OnlyContain(d => d.Shifts.Single().Code == "A-MORNING");
        result.Days.Should().OnlyContain(d => d.Shifts.Single().IsUncovered);
    }

    [Fact]
    public async Task Handle_LoadsConfirmationTotals_InSingleBatchedCall()
    {
        // Arrange
        var first = MakeOrder("PO-001", new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc));
        var second = MakeOrder("PO-002", new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc));
        Arrange(orders: [first, second]);

        // Act
        await CreateSut().Handle(new GetDispatchBoardRequest(From, To), CancellationToken.None);

        // Assert - exactly one grouped query for all shown order ids, no per-order fan-out.
        _confirmations.Verify(
            r => r.GetTotalsForOrdersAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids.Count == 2 && ids.Contains(first.Id) && ids.Contains(second.Id)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _confirmations.Verify(
            r => r.GetTotalsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
