using AsistOff.MES.Configuration.Application.Features.Machines;
using AsistOff.MES.Configuration.Application.Features.Machines.Create;
using AsistOff.MES.Configuration.Application.Features.Machines.Update;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class MachineCapacityEfficiencyTests
{
    private readonly Mock<IMachinesRepository> _repository = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<ITenantContext> _tenant = new();

    public MachineCapacityEfficiencyTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _tenant.SetupGet(t => t.TenantId).Returns(Guid.NewGuid());
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Machine>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Machine m, CancellationToken _) => m);
    }

    private CreateMachineRequestHandler CreateSut() =>
        new(_repository.Object, _guids.Object, _tenant.Object);

    private static Machine Machine() => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        Code = "M1",
        Name = "Machine",
        IsActive = true,
        Capacity = 1,
        EfficiencyFactor = 1.0m
    };

    [Fact]
    public async Task Handle_OmittedCapacityAndEfficiency_StoresDefaults()
    {
        // Arrange
        Machine? persisted = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Machine>(), It.IsAny<CancellationToken>()))
            .Callback<Machine, CancellationToken>((m, _) => persisted = m)
            .ReturnsAsync((Machine m, CancellationToken _) => m);

        // Act
        var result = await CreateSut().Handle(
            new CreateMachineRequest("M1", "Machine", null, true, null, null),
            CancellationToken.None);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.Capacity.Should().Be(1m);
        persisted.EfficiencyFactor.Should().Be(1.0m);
        result.Capacity.Should().Be(1m);
        result.EfficiencyFactor.Should().Be(1.0m);
    }

    [Fact]
    public async Task Handle_SuppliedCapacityAndEfficiency_PersistsValues()
    {
        // Arrange
        Machine? persisted = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Machine>(), It.IsAny<CancellationToken>()))
            .Callback<Machine, CancellationToken>((m, _) => persisted = m)
            .ReturnsAsync((Machine m, CancellationToken _) => m);

        // Act
        var result = await CreateSut().Handle(
            new CreateMachineRequest("M1", "Machine", null, true, null, null, 2.5m, 0.85m),
            CancellationToken.None);

        // Assert
        persisted!.Capacity.Should().Be(2.5m);
        persisted.EfficiencyFactor.Should().Be(0.85m);
        result.Capacity.Should().Be(2.5m);
        result.EfficiencyFactor.Should().Be(0.85m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-2.5)]
    public async Task Handle_InvalidCapacity_ThrowsValidationException(decimal capacity)
    {
        // Arrange
        var request = new CreateMachineRequest("M1", "Machine", null, true, null, null, capacity, 1.0m);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    [InlineData(1.0001)]
    [InlineData(1.5)]
    public async Task Handle_EfficiencyOutsideRange_ThrowsValidationException(decimal efficiency)
    {
        // Arrange
        var request = new CreateMachineRequest("M1", "Machine", null, true, null, null, 1m, efficiency);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Update_SuppliedCapacityAndEfficiency_PersistsValues()
    {
        // Arrange
        var machine = Machine();
        _repository
            .Setup(r => r.GetByIdAsync(machine.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(machine);

        // Act
        await new UpdateMachineRequestHandler(_repository.Object).Handle(
            new UpdateMachineRequest(machine.Id, "M1", "Machine", null, true, null, null, 2.5m, 0.85m),
            CancellationToken.None);

        // Assert
        machine.Capacity.Should().Be(2.5m);
        machine.EfficiencyFactor.Should().Be(0.85m);
        _repository.Verify(r => r.UpdateAsync(machine, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Update_InvalidCapacity_ThrowsValidationException(decimal capacity)
    {
        // Arrange
        var machine = Machine();
        _repository
            .Setup(r => r.GetByIdAsync(machine.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(machine);

        // Act
        var act = () => new UpdateMachineRequestHandler(_repository.Object).Handle(
            new UpdateMachineRequest(machine.Id, "M1", "Machine", null, true, null, null, capacity, 1.0m),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1.5)]
    public async Task Update_EfficiencyOutsideRange_ThrowsValidationException(decimal efficiency)
    {
        // Arrange
        var machine = Machine();
        _repository
            .Setup(r => r.GetByIdAsync(machine.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(machine);

        // Act
        var act = () => new UpdateMachineRequestHandler(_repository.Object).Handle(
            new UpdateMachineRequest(machine.Id, "M1", "Machine", null, true, null, null, 1m, efficiency),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public void ResolveCapacity_BoundaryOne_Accepts()
    {
        // Act
        var value = MachineCapacityRules.ResolveCapacity(0.0001m);

        // Assert
        value.Should().Be(0.0001m);
    }

    [Fact]
    public void ResolveEfficiencyFactor_BoundaryOne_Accepts()
    {
        // Act
        var value = MachineCapacityRules.ResolveEfficiencyFactor(1m);

        // Assert
        value.Should().Be(1m);
    }
}
