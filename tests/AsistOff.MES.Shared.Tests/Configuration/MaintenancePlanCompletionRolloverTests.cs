using AsistOff.MES.Configuration.Application.Features.MaintenancePlans;
using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Complete;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class MaintenancePlanCompletionRolloverTests
{
    private readonly Mock<IMaintenanceWorkOrdersRepository> _orders = new();
    private readonly Mock<IMaintenancePlansRepository> _plans = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly DateTime _now = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
    private readonly Guid _orderId = Guid.NewGuid();
    private readonly Guid _planId = Guid.NewGuid();

    public MaintenancePlanCompletionRolloverTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
    }

    private static MaintenancePlan TimePlan(Guid id) => new()
    {
        Id = id,
        Code = "PM-1",
        Name = "Monthly greasing",
        MachineId = Guid.NewGuid(),
        TriggerType = MaintenancePlanTriggerType.Time,
        IntervalDays = 30,
        NextDueAt = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc),
        LastCompletedAt = null,
        IsActive = true
    };

    private static MaintenancePlan MeterPlan(Guid id) => new()
    {
        Id = id,
        Code = "PM-2",
        Name = "Filter per running hours",
        MachineId = Guid.NewGuid(),
        TriggerType = MaintenancePlanTriggerType.Meter,
        IntervalDays = null,
        MeterIntervalValue = 500m,
        NextDueAt = null,
        LastCompletedAt = null,
        IsActive = true
    };

    private MaintenanceWorkOrder LinkedOrder() => new()
    {
        Id = _orderId,
        Code = "WO-1",
        Title = "Preventive maintenance Monthly greasing",
        MachineId = Guid.NewGuid(),
        PlanId = _planId,
        Priority = MaintenanceWorkOrderPriority.Medium,
        Status = MaintenanceWorkOrderStatus.Open,
        ReportedAt = _now.AddDays(-1)
    };

    private CompleteMaintenanceWorkOrderRequestHandler Sut()
        => new(_orders.Object, _plans.Object, _clock.Object);

    [Fact]
    public async Task Handle_PlanLinkedTimeOrder_RollsNextDueAtForwardByIntervalDays()
    {
        // Arrange
        var order = LinkedOrder();
        var plan = TimePlan(_planId);
        _orders.Setup(r => r.GetByIdAsync(_orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _plans.Setup(r => r.GetByIdAsync(_planId, It.IsAny<CancellationToken>())).ReturnsAsync(plan);

        // Act
        var result = await Sut().Handle(
            new CompleteMaintenanceWorkOrderRequest(_orderId, "Greased bearings"), CancellationToken.None);

        // Assert
        result.Status.Should().Be(MaintenanceWorkOrderStatus.Done);
        plan.LastCompletedAt.Should().Be(_now);
        plan.NextDueAt.Should().Be(_now.AddDays(30));
        _plans.Verify(r => r.UpdateAsync(plan, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PlanLinkedMeterOrder_AdvancesLastCompletedOnly()
    {
        // Arrange
        var order = LinkedOrder();
        var plan = MeterPlan(_planId);
        _orders.Setup(r => r.GetByIdAsync(_orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _plans.Setup(r => r.GetByIdAsync(_planId, It.IsAny<CancellationToken>())).ReturnsAsync(plan);

        // Act
        await Sut().Handle(
            new CompleteMaintenanceWorkOrderRequest(_orderId, "Replaced filter"), CancellationToken.None);

        // Assert
        plan.LastCompletedAt.Should().Be(_now);
        plan.NextDueAt.Should().BeNull();
        _plans.Verify(r => r.UpdateAsync(plan, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_StandaloneOrder_LeavesAllPlansUntouched()
    {
        // Arrange
        var order = LinkedOrder();
        order.PlanId = null;
        _orders.Setup(r => r.GetByIdAsync(_orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act
        var result = await Sut().Handle(
            new CompleteMaintenanceWorkOrderRequest(_orderId, "Fixed breakdown"), CancellationToken.None);

        // Assert
        result.Status.Should().Be(MaintenanceWorkOrderStatus.Done);
        _plans.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _plans.Verify(r => r.UpdateAsync(It.IsAny<MaintenancePlan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownPlanId_ThrowsNotFoundException()
    {
        // Arrange — plan lookup misses (e.g. a cross-tenant PlanId hidden by
        // the global query filter).
        var order = LinkedOrder();
        _orders.Setup(r => r.GetByIdAsync(_orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _plans.Setup(r => r.GetByIdAsync(_planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MaintenancePlan?)null);

        // Act
        var act = () => Sut().Handle(
            new CompleteMaintenanceWorkOrderRequest(_orderId, "Greased bearings"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public void RolloverApply_TimePlan_SetsLastCompletedAndRollsNextDue()
    {
        // Arrange
        var plan = TimePlan(Guid.NewGuid());

        // Act
        MaintenancePlanRollover.Apply(plan, _now);

        // Assert
        plan.LastCompletedAt.Should().Be(_now);
        plan.NextDueAt.Should().Be(_now.AddDays(30));
    }

    [Fact]
    public void RolloverApply_MeterPlan_KeepsNextDueAt()
    {
        // Arrange
        var plan = MeterPlan(Guid.NewGuid());

        // Act
        MaintenancePlanRollover.Apply(plan, _now);

        // Assert
        plan.LastCompletedAt.Should().Be(_now);
        plan.NextDueAt.Should().BeNull();
    }

    [Fact]
    public void DueBadge_OverduePlan_FlagsOverdueWithNegativeDueInDays()
    {
        // Arrange
        var plan = TimePlan(Guid.NewGuid());

        // Act + Assert
        MaintenancePlanDueBadge.IsOverdue(plan, _now).Should().BeTrue();
        MaintenancePlanDueBadge.DueInDays(plan, _now).Should().Be(-6);
    }

    [Fact]
    public void DueBadge_UpcomingPlan_ClearsFlagWithPositiveDueInDays()
    {
        // Arrange
        var plan = TimePlan(Guid.NewGuid());
        plan.NextDueAt = _now.AddDays(5).AddHours(12);

        // Act + Assert
        MaintenancePlanDueBadge.IsOverdue(plan, _now).Should().BeFalse();
        MaintenancePlanDueBadge.DueInDays(plan, _now).Should().Be(5);
    }

    [Fact]
    public void DueBadge_InactiveOverduePlan_NeverFlagsOverdue()
    {
        // Arrange
        var plan = TimePlan(Guid.NewGuid());
        plan.IsActive = false;

        // Act + Assert
        MaintenancePlanDueBadge.IsOverdue(plan, _now).Should().BeFalse();
    }

    [Fact]
    public void DueBadge_PlanWithoutSchedule_HasNullDueInDays()
    {
        // Arrange
        var plan = MeterPlan(Guid.NewGuid());

        // Act + Assert
        MaintenancePlanDueBadge.IsOverdue(plan, _now).Should().BeFalse();
        MaintenancePlanDueBadge.DueInDays(plan, _now).Should().BeNull();
    }
}
