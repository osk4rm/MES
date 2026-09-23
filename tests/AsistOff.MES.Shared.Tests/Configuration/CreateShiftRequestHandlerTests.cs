using AsistOff.MES.Configuration.Application.Features.Shifts.Create;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class CreateShiftRequestHandlerTests
{
    private readonly Mock<IShiftsRepository> _repository = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<ITenantContext> _tenant = new();

    public CreateShiftRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _tenant.SetupGet(t => t.TenantId).Returns(Guid.NewGuid());
        _repository
            .Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private CreateShiftRequestHandler CreateSut() =>
        new(_repository.Object, _guids.Object, _tenant.Object);

    [Fact]
    public async Task Handle_ValidRequest_PersistsShiftWithTenantIdAndTimes()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenant.SetupGet(t => t.TenantId).Returns(tenantId);

        Shift? persisted = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Shift>(), It.IsAny<CancellationToken>()))
            .Callback<Shift, CancellationToken>((entity, _) => persisted = entity)
            .ReturnsAsync((Shift entity, CancellationToken _) => entity);

        var request = new CreateShiftRequest(
            "S1", "Morning", "First shift", new TimeOnly(6, 0), new TimeOnly(14, 0), true);

        // Act
        var result = await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.TenantId.Should().Be(tenantId);
        persisted.Code.Should().Be("S1");
        persisted.StartTime.Should().Be(new TimeOnly(6, 0));
        persisted.EndTime.Should().Be(new TimeOnly(14, 0));
        result.Code.Should().Be("S1");
        result.StartTime.Should().Be(new TimeOnly(6, 0));
    }

    [Fact]
    public async Task Handle_OvernightWindow_IsAccepted()
    {
        // Arrange
        Shift? persisted = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Shift>(), It.IsAny<CancellationToken>()))
            .Callback<Shift, CancellationToken>((entity, _) => persisted = entity)
            .ReturnsAsync((Shift entity, CancellationToken _) => entity);

        // EndTime <= StartTime means the shift crosses midnight.
        var request = new CreateShiftRequest(
            "N1", "Night", null, new TimeOnly(22, 0), new TimeOnly(6, 0), true);

        // Act
        var result = await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.EndTime.Should().Be(new TimeOnly(6, 0));
        result.Code.Should().Be("N1");
    }

    [Fact]
    public async Task Handle_EmptyCode_ThrowsValidationException()
    {
        // Arrange
        var request = new CreateShiftRequest(
            "", "Morning", null, new TimeOnly(6, 0), new TimeOnly(14, 0), true);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_EmptyName_ThrowsValidationException()
    {
        // Arrange
        var request = new CreateShiftRequest(
            "S1", "  ", null, new TimeOnly(6, 0), new TimeOnly(14, 0), true);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_DuplicateCode_ThrowsConflictException()
    {
        // Arrange
        _repository
            .Setup(r => r.CodeExistsAsync("S1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new CreateShiftRequest(
            "S1", "Morning", null, new TimeOnly(6, 0), new TimeOnly(14, 0), true);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }
}
