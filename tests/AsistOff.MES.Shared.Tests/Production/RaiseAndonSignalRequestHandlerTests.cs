using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.AndonSignals.Raise;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class RaiseAndonSignalRequestHandlerTests
{
    private readonly Mock<IAndonSignalsRepository> _repository = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    public RaiseAndonSignalRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
        _repository.Setup(r => r.HasActiveSignalAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private RaiseAndonSignalRequestHandler CreateSut() =>
        new(_repository.Object, _guids.Object, _clock.Object, _tenant.Object);

    private static RaiseAndonSignalRequest ValidRequest(DateTime? raisedAt = null) => new(
        Guid.NewGuid(),
        AndonSignalCategory.Downtime,
        null,
        raisedAt ?? new DateTime(2026, 9, 24, 11, 0, 0, DateTimeKind.Utc),
        "Belt jammed",
        null,
        null);

    [Fact]
    public async Task Handle_FutureRaisedAt_ThrowsValidationException()
    {
        // Arrange
        var request = ValidRequest(_now.AddMinutes(5));

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_SecondActiveSignalOnSameWorkCenter_ThrowsConflictException()
    {
        // Arrange
        _repository.Setup(r => r.HasActiveSignalAsync(It.IsAny<Guid>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var act = () => CreateSut().Handle(ValidRequest(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_NonNullProductionOrderId_ThrowsValidationException()
    {
        // Arrange
        var request = ValidRequest() with { ProductionOrderId = Guid.NewGuid() };

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_ValidRequest_PersistsActiveSignalWithTenantId()
    {
        // Arrange
        var request = ValidRequest();
        AndonSignal? saved = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<AndonSignal>(), It.IsAny<CancellationToken>()))
            .Callback<AndonSignal, CancellationToken>((e, _) => saved = e)
            .ReturnsAsync((AndonSignal e, CancellationToken _) => e);

        // Act
        var result = await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        saved.Should().NotBeNull();
        saved!.MachineId.Should().Be(request.MachineId);
        saved.Category.Should().Be(AndonSignalCategory.Downtime);
        saved.Status.Should().Be(AndonSignalStatus.Active);
        saved.TenantId.Should().Be(_tenantId);
        result.Status.Should().Be(AndonSignalStatus.Active);
        result.MachineId.Should().Be(request.MachineId);
    }
}
