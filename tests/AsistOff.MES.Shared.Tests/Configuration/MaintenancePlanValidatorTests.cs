using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Create;
using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Update;
using AsistOff.MES.Configuration.Domain.Enums;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class MaintenancePlanValidatorTests
{
    private readonly CreateMaintenancePlanRequestValidator _createValidator = new();
    private readonly UpdateMaintenancePlanRequestValidator _updateValidator = new();
    private static readonly Guid MachineId = Guid.NewGuid();
    private static readonly DateTime Future = DateTime.UtcNow.AddDays(30);

    private static CreateMaintenancePlanRequest ValidCreate() =>
        new("PM-1", "Monthly greasing", null, MachineId,
            MaintenancePlanTriggerType.Time, 30, null, Future);

    private static UpdateMaintenancePlanRequest ValidUpdate() =>
        new(Guid.NewGuid(), "PM-1", "Monthly greasing", null, MachineId,
            MaintenancePlanTriggerType.Time, 30, null, Future, true);

    [Fact]
    public void Validate_ValidTimeRequest_IsValid()
    {
        _createValidator.Validate(ValidCreate()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ValidMeterRequest_IsValid()
    {
        var request = ValidCreate() with
        {
            TriggerType = MaintenancePlanTriggerType.Meter,
            IntervalDays = null,
            MeterIntervalValue = 500m,
            NextDueAt = null
        };

        _createValidator.Validate(request).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyCode_IsInvalid(string code)
    {
        _createValidator.Validate(ValidCreate() with { Code = code }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_MissingMachine_IsInvalid()
    {
        _createValidator.Validate(ValidCreate() with { MachineId = Guid.Empty }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_UndefinedTriggerType_IsInvalid()
    {
        var request = ValidCreate() with { TriggerType = (MaintenancePlanTriggerType)99 };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_TimePlanWithoutInterval_IsInvalid()
    {
        _createValidator.Validate(ValidCreate() with { IntervalDays = 0 }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_TimePlanWithoutNextDueAt_IsInvalid()
    {
        _createValidator.Validate(ValidCreate() with { NextDueAt = null }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_MeterPlanWithoutMeterInterval_IsInvalid()
    {
        var request = ValidCreate() with
        {
            TriggerType = MaintenancePlanTriggerType.Meter,
            IntervalDays = null,
            MeterIntervalValue = null,
            NextDueAt = null
        };

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ValidUpdateRequest_IsValid()
    {
        _updateValidator.Validate(ValidUpdate()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_UpdateWithEmptyId_IsInvalid()
    {
        _updateValidator.Validate(ValidUpdate() with { Id = Guid.Empty }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_UpdateMeterPlanWithoutMeterInterval_IsInvalid()
    {
        var request = ValidUpdate() with
        {
            TriggerType = MaintenancePlanTriggerType.Meter,
            IntervalDays = null,
            MeterIntervalValue = 0m,
            NextDueAt = null
        };

        _updateValidator.Validate(request).IsValid.Should().BeFalse();
    }
}
