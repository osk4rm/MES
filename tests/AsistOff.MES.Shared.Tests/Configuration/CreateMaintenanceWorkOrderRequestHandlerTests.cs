using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Create;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class CreateMaintenanceWorkOrderRequestHandlerTests
{
    private readonly Mock<IMaintenanceWorkOrdersRepository> _repository = new();
    private readonly Mock<IMachinesRepository> _machines = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _machineId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    public CreateMaintenanceWorkOrderRequestHandlerTests()
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

    private CreateMaintenanceWorkOrderRequestHandler CreateSut() =>
        new(_repository.Object, _machines.Object, _guids.Object, _clock.Object, _tenant.Object);

    private static CreateMaintenanceWorkOrderRequest ValidRequest(Guid machineId) =>
        new("WO-1", "Fix spindle", "Spindle vibrates", machineId, MaintenanceWorkOrderPriority.High);

    [Fact]
    public async Task Handle_EmptyCode_ThrowsValidationException()
    {
        var request = ValidRequest(_machineId) with { Code = "  " };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_EmptyTitle_ThrowsValidationException()
    {
        var request = ValidRequest(_machineId) with { Title = "" };

        var act = () => CreateSut().Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_UndefinedPriority_ThrowsValidationException()
    {
        var request = ValidRequest(_machineId) with { Priority = (MaintenanceWorkOrderPriority)99 };

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

        var act = () => CreateSut().Handle(ValidRequest(unknownMachineId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_DuplicateCode_ThrowsConflictException()
    {
        _repository
            .Setup(r => r.CodeExistsAsync("WO-1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => CreateSut().Handle(ValidRequest(_machineId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_ValidRequest_PersistsOpenOrderWithTenantId()
    {
        MaintenanceWorkOrder? persisted = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<MaintenanceWorkOrder>(), It.IsAny<CancellationToken>()))
            .Callback<MaintenanceWorkOrder, CancellationToken>((entity, _) => persisted = entity)
            .ReturnsAsync((MaintenanceWorkOrder entity, CancellationToken _) => entity);

        var result = await CreateSut().Handle(ValidRequest(_machineId), CancellationToken.None);

        persisted.Should().NotBeNull();
        persisted!.TenantId.Should().Be(_tenantId);
        persisted.MachineId.Should().Be(_machineId);
        persisted.Status.Should().Be(MaintenanceWorkOrderStatus.Open);
        persisted.ReportedAt.Should().Be(_now);
        persisted.StartedAt.Should().BeNull();
        persisted.CompletedAt.Should().BeNull();
        result.Status.Should().Be(MaintenanceWorkOrderStatus.Open);
        result.MachineId.Should().Be(_machineId);
    }
}
