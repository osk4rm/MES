using AsistOff.MES.Configuration.Application.Features.MaintenancePlans;
using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.EvaluateDue;
using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.RaiseNow;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Users.Core.Rbac;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

/// <summary>
/// Unit tests for slice 2/3 (issue #298): due selection plus the idempotency
/// guard that prevents a second Open work order for the same plan.
/// </summary>
public class MaintenancePlanDueEvaluationTests
{
    private readonly Mock<IMaintenancePlansRepository> _plans = new();
    private readonly Mock<IMaintenanceWorkOrdersRepository> _workOrders = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _machineId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

    public MaintenancePlanDueEvaluationTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _workOrders
            .Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _workOrders
            .Setup(r => r.AddAsync(It.IsAny<MaintenanceWorkOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MaintenanceWorkOrder entity, CancellationToken _) => entity);
    }

    private static MaintenancePlan TimePlan(Guid machineId, DateTime? nextDueAt, bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        Code = $"PM-{Guid.NewGuid():N}"[..12],
        Name = "Monthly greasing",
        MachineId = machineId,
        TriggerType = MaintenancePlanTriggerType.Time,
        IntervalDays = 30,
        NextDueAt = nextDueAt,
        IsActive = isActive,
    };

    private static MaintenancePlan MeterPlan(Guid machineId, decimal interval, bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        Code = $"PM-{Guid.NewGuid():N}"[..12],
        Name = "Filter per hours",
        MachineId = machineId,
        TriggerType = MaintenancePlanTriggerType.Meter,
        MeterIntervalValue = interval,
        NextDueAt = null,
        IsActive = isActive,
    };

    private EvaluateDueMaintenancePlansRequestHandler CreateEvaluateSut() =>
        new(_plans.Object, _workOrders.Object, _guids.Object, _clock.Object, _tenant.Object);

    private RaiseMaintenancePlanNowRequestHandler CreateRaiseSut() =>
        new(_plans.Object, _workOrders.Object, _guids.Object, _clock.Object, _tenant.Object);

    private void SetupPlans(params MaintenancePlan[] plans)
    {
        _plans
            .Setup(r => r.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plans.ToList());
        _workOrders
            .Setup(r => r.ListOpenByPlanIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MaintenanceWorkOrder>());
    }

    [Fact]
    public void IsDue_DueTimePlan_ReturnsTrue()
    {
        var plan = TimePlan(_machineId, _now.AddHours(-1));

        MaintenancePlanDueEvaluator.IsDue(plan, _now, null).Should().BeTrue();
    }

    [Fact]
    public void IsDue_NotYetDueTimePlan_ReturnsFalse()
    {
        var plan = TimePlan(_machineId, _now.AddDays(30));

        MaintenancePlanDueEvaluator.IsDue(plan, _now, null).Should().BeFalse();
    }

    [Fact]
    public void IsDue_InactiveTimePlan_ReturnsFalse()
    {
        var plan = TimePlan(_machineId, _now.AddHours(-1), isActive: false);

        MaintenancePlanDueEvaluator.IsDue(plan, _now, null).Should().BeFalse();
    }

    [Fact]
    public void IsDue_DueMeterPlan_ReturnsTrue()
    {
        var plan = MeterPlan(_machineId, 500m);

        MaintenancePlanDueEvaluator.IsDue(plan, _now, 600m).Should().BeTrue();
    }

    [Fact]
    public void IsDue_MeterPlanBelowThreshold_ReturnsFalse()
    {
        var plan = MeterPlan(_machineId, 500m);

        MaintenancePlanDueEvaluator.IsDue(plan, _now, 100m).Should().BeFalse();
    }

    [Fact]
    public void IsDue_MeterPlanWithoutReading_ReturnsFalse()
    {
        var plan = MeterPlan(_machineId, 500m);

        MaintenancePlanDueEvaluator.IsDue(plan, _now, null).Should().BeFalse();
    }

    [Fact]
    public void IsDue_InactiveMeterPlan_ReturnsFalse()
    {
        var plan = MeterPlan(_machineId, 500m, isActive: false);

        MaintenancePlanDueEvaluator.IsDue(plan, _now, 9000m).Should().BeFalse();
    }

    [Fact]
    public void ResolveMeterReading_PerMachineReadingWinsOverGlobal()
    {
        var plan = MeterPlan(_machineId, 500m);
        var readings = new Dictionary<Guid, decimal> { [_machineId] = 111m };

        MaintenancePlanDueEvaluator.ResolveMeterReading(plan, 999m, readings).Should().Be(111m);
    }

    [Fact]
    public void ResolveMeterReading_FallsBackToGlobalReading()
    {
        var plan = MeterPlan(_machineId, 500m);

        MaintenancePlanDueEvaluator.ResolveMeterReading(plan, 999m, null).Should().Be(999m);
    }

    [Fact]
    public async Task Handle_DueTimePlan_CreatesOneOpenWorkOrderWithPlanLink()
    {
        var due = TimePlan(_machineId, _now.AddHours(-1));
        var future = TimePlan(_machineId, _now.AddDays(30));
        SetupPlans(due, future);
        MaintenanceWorkOrder? persisted = null;
        _workOrders
            .Setup(r => r.AddAsync(It.IsAny<MaintenanceWorkOrder>(), It.IsAny<CancellationToken>()))
            .Callback<MaintenanceWorkOrder, CancellationToken>((entity, _) => persisted = entity)
            .ReturnsAsync((MaintenanceWorkOrder entity, CancellationToken _) => entity);

        var result = await CreateEvaluateSut().Handle(
            new EvaluateDueMaintenancePlansRequest(), CancellationToken.None);

        result.Should().ContainSingle(o => o.PlanId == due.Id);
        result.Should().NotContain(o => o.PlanId == future.Id);
        persisted.Should().NotBeNull();
        persisted!.PlanId.Should().Be(due.Id);
        persisted.MachineId.Should().Be(_machineId);
        persisted.TenantId.Should().Be(_tenantId);
        persisted.Status.Should().Be(MaintenanceWorkOrderStatus.Open);
        persisted.Code.Should().StartWith(due.Code.ToUpperInvariant());
        _workOrders.Verify(
            r => r.AddAsync(It.IsAny<MaintenanceWorkOrder>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DueMeterPlanWithReading_CreatesWorkOrder_AndSkipsOthersWithoutReading()
    {
        var due = MeterPlan(_machineId, 50m);
        var expensive = MeterPlan(_machineId, 5000m);
        SetupPlans(due, expensive);

        var result = await CreateEvaluateSut().Handle(
            new EvaluateDueMaintenancePlansRequest(CurrentMeterReading: 100m),
            CancellationToken.None);

        result.Should().ContainSingle(o => o.PlanId == due.Id);
        result.Should().NotContain(o => o.PlanId == expensive.Id);
    }

    [Fact]
    public async Task Handle_MeterPlansWithoutReading_CreatesNothing()
    {
        SetupPlans(MeterPlan(_machineId, 50m));

        var result = await CreateEvaluateSut().Handle(
            new EvaluateDueMaintenancePlansRequest(), CancellationToken.None);

        result.Should().BeEmpty();
        _workOrders.Verify(
            r => r.AddAsync(It.IsAny<MaintenanceWorkOrder>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_PlanWithLiveOrder_DoesNotDuplicate()
    {
        var due = TimePlan(_machineId, _now.AddHours(-1));
        SetupPlans(due);
        _workOrders
            .Setup(r => r.ListOpenByPlanIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new MaintenanceWorkOrder
                {
                    Id = Guid.NewGuid(),
                    Code = "WO-EXISTING",
                    Title = "Existing",
                    MachineId = _machineId,
                    PlanId = due.Id,
                    Priority = MaintenanceWorkOrderPriority.Medium,
                    Status = MaintenanceWorkOrderStatus.Open,
                    ReportedAt = _now.AddHours(-2),
                },
            });

        var result = await CreateEvaluateSut().Handle(
            new EvaluateDueMaintenancePlansRequest(), CancellationToken.None);

        result.Should().BeEmpty();
        _workOrders.Verify(
            r => r.AddAsync(It.IsAny<MaintenanceWorkOrder>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_InProgressOrder_BlocksDuplicate()
    {
        var due = TimePlan(_machineId, _now.AddHours(-1));
        SetupPlans(due);
        _workOrders
            .Setup(r => r.ListOpenByPlanIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new MaintenanceWorkOrder
                {
                    Id = Guid.NewGuid(),
                    Code = "WO-WIP",
                    Title = "In progress",
                    MachineId = _machineId,
                    PlanId = due.Id,
                    Priority = MaintenanceWorkOrderPriority.Medium,
                    Status = MaintenanceWorkOrderStatus.InProgress,
                    ReportedAt = _now.AddHours(-2),
                },
            });

        var result = await CreateEvaluateSut().Handle(
            new EvaluateDueMaintenancePlansRequest(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NoActivePlans_ReturnsEmpty()
    {
        SetupPlans();

        var result = await CreateEvaluateSut().Handle(
            new EvaluateDueMaintenancePlansRequest(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task RaiseNow_UnknownId_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        _plans
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MaintenancePlan?)null);

        var act = () => CreateRaiseSut().Handle(
            new RaiseMaintenancePlanNowRequest(id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RaiseNow_InactivePlan_ThrowsValidationException()
    {
        var plan = TimePlan(_machineId, _now.AddDays(30), isActive: false);
        _plans
            .Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var act = () => CreateRaiseSut().Handle(
            new RaiseMaintenancePlanNowRequest(plan.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task RaiseNow_NotDuePlan_CreatesWorkOrderWithPlanLink()
    {
        var plan = TimePlan(_machineId, _now.AddDays(30));
        _plans
            .Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _workOrders
            .Setup(r => r.ListOpenByPlanIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MaintenanceWorkOrder>());
        MaintenanceWorkOrder? persisted = null;
        _workOrders
            .Setup(r => r.AddAsync(It.IsAny<MaintenanceWorkOrder>(), It.IsAny<CancellationToken>()))
            .Callback<MaintenanceWorkOrder, CancellationToken>((entity, _) => persisted = entity)
            .ReturnsAsync((MaintenanceWorkOrder entity, CancellationToken _) => entity);

        var result = await CreateRaiseSut().Handle(
            new RaiseMaintenancePlanNowRequest(plan.Id), CancellationToken.None);

        result.PlanId.Should().Be(plan.Id);
        result.MachineId.Should().Be(_machineId);
        result.Status.Should().Be(MaintenanceWorkOrderStatus.Open);
        persisted.Should().NotBeNull();
        persisted!.TenantId.Should().Be(_tenantId);
        persisted.PlanId.Should().Be(plan.Id);
    }

    [Fact]
    public async Task RaiseNow_ExistingLiveOrder_ReturnsExistingWithoutDuplicate()
    {
        var plan = TimePlan(_machineId, _now.AddDays(30));
        var existing = new MaintenanceWorkOrder
        {
            Id = Guid.NewGuid(),
            Code = "WO-LIVE",
            Title = "Live",
            MachineId = _machineId,
            PlanId = plan.Id,
            Priority = MaintenanceWorkOrderPriority.Medium,
            Status = MaintenanceWorkOrderStatus.Open,
            ReportedAt = _now.AddHours(-1),
        };
        _plans
            .Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _workOrders
            .Setup(r => r.ListOpenByPlanIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { existing });

        var result = await CreateRaiseSut().Handle(
            new RaiseMaintenancePlanNowRequest(plan.Id), CancellationToken.None);

        result.Id.Should().Be(existing.Id);
        result.PlanId.Should().Be(plan.Id);
        _workOrders.Verify(
            r => r.AddAsync(It.IsAny<MaintenanceWorkOrder>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Requests_ImplementTenantRequest_AndRequireConfigurationWrite()
    {
        typeof(EvaluateDueMaintenancePlansRequest).GetInterfaces()
            .Should().Contain(i => i.Name.StartsWith("ITenantRequest"));
        typeof(RaiseMaintenancePlanNowRequest).GetInterfaces()
            .Should().Contain(i => i.Name.StartsWith("ITenantRequest"));

        typeof(EvaluateDueMaintenancePlansRequest).GetCustomAttributes(typeof(RequirePermissionAttribute), false)
            .OfType<RequirePermissionAttribute>().Single().Permission
            .Should().Be(RbacDefaults.ConfigurationWrite);
        typeof(RaiseMaintenancePlanNowRequest).GetCustomAttributes(typeof(RequirePermissionAttribute), false)
            .OfType<RequirePermissionAttribute>().Single().Permission
            .Should().Be(RbacDefaults.ConfigurationWrite);
    }

    [Fact]
    public void EvaluateValidator_NegativeReading_IsInvalid()
    {
        var validator = new EvaluateDueMaintenancePlansRequestValidator();

        validator.Validate(new EvaluateDueMaintenancePlansRequest(CurrentMeterReading: -5m)).IsValid.Should().BeFalse();
        validator.Validate(new EvaluateDueMaintenancePlansRequest(MeterReading: -1m)).IsValid.Should().BeFalse();
        validator.Validate(new EvaluateDueMaintenancePlansRequest(
            MeterReadings: new Dictionary<Guid, decimal> { [_machineId] = -2m })).IsValid.Should().BeFalse();
    }

    [Fact]
    public void EvaluateValidator_ValidRequest_IsValid()
    {
        var validator = new EvaluateDueMaintenancePlansRequestValidator();

        validator.Validate(new EvaluateDueMaintenancePlansRequest(CurrentMeterReading: 100m)).IsValid.Should().BeTrue();
        validator.Validate(new EvaluateDueMaintenancePlansRequest()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void RaiseValidator_EmptyId_IsInvalid()
    {
        var validator = new RaiseMaintenancePlanNowRequestValidator();

        validator.Validate(new RaiseMaintenancePlanNowRequest(Guid.Empty)).IsValid.Should().BeFalse();
        validator.Validate(new RaiseMaintenancePlanNowRequest(Guid.NewGuid())).IsValid.Should().BeTrue();
    }
}
