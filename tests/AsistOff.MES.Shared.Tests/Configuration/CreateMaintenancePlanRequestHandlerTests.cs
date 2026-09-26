using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Create;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class CreateMaintenancePlanRequestHandlerTests
{
    private readonly Mock<IMaintenancePlansRepository> _repository = new();
    private readonly Mock<IMachinesRepository> _machines = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _machineId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

    public CreateMaintenancePlanRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
        _machines
            .Setup(r => r.GetByIdAsync(_machineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Machine { Id = _machineId, Code = "MC-1", Name = "Lathe" });
        _repository
            .Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private CreateMaintenancePlanRequestHandler CreateSut() =>
        new(_repository.Object, _machines.Object, _guids.Object, _clock.Object, _tenant.Object);

    private static CreateMaintenancePlanRequest ValidTimeRequest(Guid machineId, DateTime now) =>
        new("PM-1", "Monthly greasing", "Grease spindle bearings", machineId,
            MaintenancePlanTriggerType.Time, 30, null, now.AddDays(30));

    private static CreateMaintenancePlanRequest ValidMeterRequest(Guid machineId) =>
        new("PM-2", "Filter per hours", null, machineId,
            MaintenancePlanTriggerType.Meter, null, 500m, null);

    [Fact]
    public async Task Handle_EmptyCode_ThrowsValidationException()
    {
        var request = ValidTimeRequest(_machineId, _now) with { Code = "  " };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_EmptyName_ThrowsValidationException()
    {
        var request = ValidTimeRequest(_machineId, _now) with { Name = "" };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_UndefinedTriggerType_ThrowsValidationException()
    {
        var request = ValidTimeRequest(_machineId, _now) with { TriggerType = (MaintenancePlanTriggerType)99 };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_UnknownMachine_ThrowsNotFoundException()
    {
        var unknownMachineId = Guid.NewGuid();
        _machines
            .Setup(r => r.GetByIdAsync(unknownMachineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Machine?)null);

        var act = () => CreateSut().Handle(ValidTimeRequest(unknownMachineId, _now), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_TimePlanWithoutInterval_ThrowsValidationException()
    {
        var request = ValidTimeRequest(_machineId, _now) with { IntervalDays = 0 };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_TimePlanWithoutNextDueAt_ThrowsValidationException()
    {
        var request = ValidTimeRequest(_machineId, _now) with { NextDueAt = null };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_TimePlanWithPastNextDueAt_ThrowsValidationException()
    {
        var request = ValidTimeRequest(_machineId, _now) with { NextDueAt = _now.AddDays(-1) };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_MeterPlanWithoutMeterInterval_ThrowsValidationException()
    {
        var request = ValidMeterRequest(_machineId) with { MeterIntervalValue = 0m };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_DuplicateCode_ThrowsConflictException()
    {
        _repository
            .Setup(r => r.CodeExistsAsync("PM-1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => CreateSut().Handle(ValidTimeRequest(_machineId, _now), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_ValidTimeRequest_PersistsPlanWithTenantId()
    {
        MaintenancePlan? persisted = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<MaintenancePlan>(), It.IsAny<CancellationToken>()))
            .Callback<MaintenancePlan, CancellationToken>((entity, _) => persisted = entity)
            .ReturnsAsync((MaintenancePlan entity, CancellationToken _) => entity);

        var result = await CreateSut().Handle(ValidTimeRequest(_machineId, _now), CancellationToken.None);

        persisted.Should().NotBeNull();
        persisted!.TenantId.Should().Be(_tenantId);
        persisted.MachineId.Should().Be(_machineId);
        persisted.TriggerType.Should().Be(MaintenancePlanTriggerType.Time);
        persisted.IntervalDays.Should().Be(30);
        persisted.NextDueAt.Should().Be(_now.AddDays(30));
        persisted.LastCompletedAt.Should().BeNull();
        persisted.IsActive.Should().BeTrue();
        result.TriggerType.Should().Be(MaintenancePlanTriggerType.Time);
        result.MachineId.Should().Be(_machineId);
    }

    [Fact]
    public async Task Handle_ValidMeterRequest_PersistsPlanWithoutNextDueAt()
    {
        MaintenancePlan? persisted = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<MaintenancePlan>(), It.IsAny<CancellationToken>()))
            .Callback<MaintenancePlan, CancellationToken>((entity, _) => persisted = entity)
            .ReturnsAsync((MaintenancePlan entity, CancellationToken _) => entity);

        var result = await CreateSut().Handle(ValidMeterRequest(_machineId), CancellationToken.None);

        persisted.Should().NotBeNull();
        persisted!.TenantId.Should().Be(_tenantId);
        persisted.TriggerType.Should().Be(MaintenancePlanTriggerType.Meter);
        persisted.MeterIntervalValue.Should().Be(500m);
        persisted.NextDueAt.Should().BeNull();
        result.TriggerType.Should().Be(MaintenancePlanTriggerType.Meter);
    }
}
