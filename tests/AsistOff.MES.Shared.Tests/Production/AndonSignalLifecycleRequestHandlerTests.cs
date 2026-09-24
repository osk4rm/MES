using AsistOff.MES.Production.Application.Features.AndonSignals.Acknowledge;
using AsistOff.MES.Production.Application.Features.AndonSignals.Resolve;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class AcknowledgeAndonSignalRequestHandlerTests
{
    private readonly Mock<IAndonSignalsRepository> _repository = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    public AcknowledgeAndonSignalRequestHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
    }

    private AcknowledgeAndonSignalRequestHandler CreateSut() =>
        new(_repository.Object, _clock.Object);

    private static AndonSignal Signal(AndonSignalStatus status) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        MachineId = Guid.NewGuid(),
        Category = AndonSignalCategory.Quality,
        Status = status,
        RaisedAt = new DateTime(2026, 9, 24, 11, 0, 0, DateTimeKind.Utc),
        CreatedAt = new DateTime(2026, 9, 24, 11, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task Handle_NonActiveSignal_ThrowsConflictException()
    {
        // Arrange
        _repository.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Signal(AndonSignalStatus.Acknowledged));

        // Act
        var act = () => CreateSut().Handle(new AcknowledgeAndonSignalRequest(Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFoundException()
    {
        // Arrange
        _repository.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AndonSignal?)null);

        // Act
        var act = () => CreateSut().Handle(new AcknowledgeAndonSignalRequest(Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ActiveSignal_SetsAcknowledgedAtAndStatus()
    {
        // Arrange
        var signal = Signal(AndonSignalStatus.Active);
        _repository.Setup(r => r.GetAsync(signal.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(signal);

        // Act
        var result = await CreateSut().Handle(new AcknowledgeAndonSignalRequest(signal.Id), CancellationToken.None);

        // Assert
        result.Status.Should().Be(AndonSignalStatus.Acknowledged);
        result.AcknowledgedAt.Should().Be(_now);
        _repository.Verify(r => r.UpdateAsync(signal, It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class ResolveAndonSignalRequestHandlerTests
{
    private readonly Mock<IAndonSignalsRepository> _repository = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly DateTime _raisedAt = new(2026, 9, 24, 11, 0, 0, DateTimeKind.Utc);
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    public ResolveAndonSignalRequestHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
    }

    private ResolveAndonSignalRequestHandler CreateSut() =>
        new(_repository.Object, _clock.Object);

    private AndonSignal Signal(AndonSignalStatus status) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        MachineId = Guid.NewGuid(),
        Category = AndonSignalCategory.Material,
        Status = status,
        RaisedAt = _raisedAt,
        CreatedAt = _raisedAt
    };

    [Fact]
    public async Task Handle_ResolvedAtEarlierThanRaisedAt_ThrowsValidationException()
    {
        // Arrange
        var signal = Signal(AndonSignalStatus.Active);
        _repository.Setup(r => r.GetAsync(signal.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(signal);

        // Act
        var act = () => CreateSut().Handle(
            new ResolveAndonSignalRequest(signal.Id, _raisedAt.AddMinutes(-1)), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_AlreadyResolvedSignal_ThrowsConflictException()
    {
        // Arrange
        var signal = Signal(AndonSignalStatus.Resolved);
        _repository.Setup(r => r.GetAsync(signal.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(signal);

        // Act
        var act = () => CreateSut().Handle(
            new ResolveAndonSignalRequest(signal.Id, _now), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_AcknowledgedSignal_SetsResolvedAtAndStatus()
    {
        // Arrange
        var signal = Signal(AndonSignalStatus.Acknowledged);
        _repository.Setup(r => r.GetAsync(signal.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(signal);

        // Act
        var result = await CreateSut().Handle(
            new ResolveAndonSignalRequest(signal.Id, _now), CancellationToken.None);

        // Assert
        result.Status.Should().Be(AndonSignalStatus.Resolved);
        result.ResolvedAt.Should().Be(_now);
        _repository.Verify(r => r.UpdateAsync(signal, It.IsAny<CancellationToken>()), Times.Once);
    }
}
