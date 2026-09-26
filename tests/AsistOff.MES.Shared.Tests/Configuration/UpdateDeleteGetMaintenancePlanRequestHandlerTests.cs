using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Delete;
using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Get;
using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Update;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class UpdateDeleteGetMaintenancePlanRequestHandlerTests
{
    private readonly Mock<IMaintenancePlansRepository> _repository = new();
    private readonly Mock<IMachinesRepository> _machines = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Guid _machineId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

    public UpdateDeleteGetMaintenancePlanRequestHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _machines
            .Setup(r => r.GetByIdAsync(_machineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Machine { Id = _machineId, Code = "MC-1", Name = "Lathe" });
        _repository
            .Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private static MaintenancePlan Plan(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        Code = "PM-1",
        Name = "Monthly greasing",
        MachineId = Guid.NewGuid(),
        TriggerType = MaintenancePlanTriggerType.Time,
        IntervalDays = 30,
        NextDueAt = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc),
        IsActive = true
    };

    private UpdateMaintenancePlanRequest ValidUpdate(Guid id) =>
        new(id, "PM-1", "Monthly greasing", "desc", _machineId,
            MaintenancePlanTriggerType.Time, 30, null, _now.AddDays(30), true);

    [Fact]
    public async Task Get_UnknownId_ThrowsNotFoundException()
    {
        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MaintenancePlan?)null);

        var act = () => new GetMaintenancePlanRequestHandler(_repository.Object)
            .Handle(new GetMaintenancePlanRequest(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Get_ExistingId_ReturnsMappedPlan()
    {
        var plan = Plan();
        _repository
            .Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await new GetMaintenancePlanRequestHandler(_repository.Object)
            .Handle(new GetMaintenancePlanRequest(plan.Id), CancellationToken.None);

        result.Id.Should().Be(plan.Id);
        result.Code.Should().Be("PM-1");
        result.TriggerType.Should().Be(MaintenancePlanTriggerType.Time);
    }

    [Fact]
    public async Task Update_UnknownId_ThrowsNotFoundException()
    {
        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MaintenancePlan?)null);

        var id = Guid.NewGuid();
        var act = () => new UpdateMaintenancePlanRequestHandler(_repository.Object, _machines.Object, _clock.Object)
            .Handle(ValidUpdate(id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Update_DuplicateCode_ThrowsConflictException()
    {
        var plan = Plan();
        _repository
            .Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _repository
            .Setup(r => r.CodeExistsAsync("PM-2", plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => new UpdateMaintenancePlanRequestHandler(_repository.Object, _machines.Object, _clock.Object)
            .Handle(ValidUpdate(plan.Id) with { Code = "PM-2" }, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Update_UnknownMachine_ThrowsNotFoundException()
    {
        var plan = Plan();
        var unknownMachineId = Guid.NewGuid();
        _repository
            .Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _machines
            .Setup(r => r.GetByIdAsync(unknownMachineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Machine?)null);

        var act = () => new UpdateMaintenancePlanRequestHandler(_repository.Object, _machines.Object, _clock.Object)
            .Handle(ValidUpdate(plan.Id) with { MachineId = unknownMachineId }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Update_PastNextDueAt_ThrowsValidationException()
    {
        var plan = Plan();
        _repository
            .Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var act = () => new UpdateMaintenancePlanRequestHandler(_repository.Object, _machines.Object, _clock.Object)
            .Handle(ValidUpdate(plan.Id) with { NextDueAt = _now.AddDays(-1) }, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Update_ValidRequest_PersistsChanges()
    {
        var plan = Plan();
        _repository
            .Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        await new UpdateMaintenancePlanRequestHandler(_repository.Object, _machines.Object, _clock.Object)
            .Handle(ValidUpdate(plan.Id) with { Name = "Weekly greasing", IsActive = false }, CancellationToken.None);

        plan.Name.Should().Be("Weekly greasing");
        plan.IsActive.Should().BeFalse();
        plan.MachineId.Should().Be(_machineId);
        _repository.Verify(r => r.UpdateAsync(plan, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_UnknownId_ThrowsNotFoundException()
    {
        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MaintenancePlan?)null);

        var act = () => new DeleteMaintenancePlanRequestHandler(_repository.Object)
            .Handle(new DeleteMaintenancePlanRequest(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Delete_ExistingId_RemovesPlan()
    {
        var plan = Plan();
        _repository
            .Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        await new DeleteMaintenancePlanRequestHandler(_repository.Object)
            .Handle(new DeleteMaintenancePlanRequest(plan.Id), CancellationToken.None);

        _repository.Verify(r => r.DeleteAsync(plan.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
