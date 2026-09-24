using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Complete;
using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Create;
using AsistOff.MES.Configuration.Domain.Enums;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class MaintenanceWorkOrderValidatorTests
{
    [Fact]
    public async Task CreateValidator_EmptyCode_IsInvalid()
    {
        var validator = new CreateMaintenanceWorkOrderValidator();
        var request = new CreateMaintenanceWorkOrderRequest(
            "", "Fix spindle", null, Guid.NewGuid(), MaintenanceWorkOrderPriority.Medium);

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.Code));
    }

    [Fact]
    public async Task CreateValidator_EmptyTitle_IsInvalid()
    {
        var validator = new CreateMaintenanceWorkOrderValidator();
        var request = new CreateMaintenanceWorkOrderRequest(
            "WO-1", "  ", null, Guid.NewGuid(), MaintenanceWorkOrderPriority.Medium);

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.Title));
    }

    [Fact]
    public async Task CreateValidator_UndefinedPriority_IsInvalid()
    {
        var validator = new CreateMaintenanceWorkOrderValidator();
        var request = new CreateMaintenanceWorkOrderRequest(
            "WO-1", "Fix spindle", null, Guid.NewGuid(), (MaintenanceWorkOrderPriority)99);

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.Priority));
    }

    [Fact]
    public async Task CreateValidator_ValidRequest_IsValid()
    {
        var validator = new CreateMaintenanceWorkOrderValidator();
        var request = new CreateMaintenanceWorkOrderRequest(
            "WO-1", "Fix spindle", "Notes", Guid.NewGuid(), MaintenanceWorkOrderPriority.Critical);

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteValidator_EmptyResolutionNotes_IsInvalid()
    {
        var validator = new CompleteMaintenanceWorkOrderValidator();
        var request = new CompleteMaintenanceWorkOrderRequest(Guid.NewGuid(), "  ");

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.ResolutionNotes));
    }

    [Fact]
    public async Task CompleteValidator_WithResolutionNotes_IsValid()
    {
        var validator = new CompleteMaintenanceWorkOrderValidator();
        var request = new CompleteMaintenanceWorkOrderRequest(Guid.NewGuid(), "Replaced bearing");

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }
}
