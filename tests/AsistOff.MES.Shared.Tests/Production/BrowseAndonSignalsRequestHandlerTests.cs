using AsistOff.MES.Production.Application.Features.AndonSignals.Browse;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Pagination;
using FluentAssertions;
using LinqKit;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class BrowseAndonSignalsRequestHandlerTests
{
    private readonly Mock<IAndonSignalsRepository> _repository = new();

    private BrowseAndonSignalsRequestHandler CreateSut() =>
        new(_repository.Object);

    private static AndonSignal Signal(Guid machineId, AndonSignalCategory category, AndonSignalStatus status, DateTime raisedAt) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        MachineId = machineId,
        Category = category,
        Status = status,
        RaisedAt = raisedAt,
        CreatedAt = raisedAt
    };

    [Fact]
    public async Task Handle_FiltersByMachineCategoryStatusAndDateRange()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        var raisedAt = new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc);
        var matching = Signal(machineId, AndonSignalCategory.Downtime, AndonSignalStatus.Active, raisedAt);
        var otherMachine = Signal(Guid.NewGuid(), AndonSignalCategory.Downtime, AndonSignalStatus.Active, raisedAt);
        var otherCategory = Signal(machineId, AndonSignalCategory.Quality, AndonSignalStatus.Active, raisedAt);
        var otherStatus = Signal(machineId, AndonSignalCategory.Downtime, AndonSignalStatus.Resolved, raisedAt);
        var outsideRange = Signal(machineId, AndonSignalCategory.Downtime, AndonSignalStatus.Active, raisedAt.AddDays(-5));

        ExpressionStarter<AndonSignal>? captured = null;
        _repository.Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<AndonSignal>>(), It.IsAny<CancellationToken>()))
            .Callback<ExpressionStarter<AndonSignal>, CancellationToken>((p, _) => captured = p)
            .ReturnsAsync(1);
        _repository.Setup(r => r.BrowseAsync(It.IsAny<Paginator<AndonSignal>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { matching });

        var request = new BrowseAndonSignalsRequest
        {
            MachineId = machineId,
            Category = AndonSignalCategory.Downtime,
            Status = AndonSignalStatus.Active,
            RaisedFrom = raisedAt.AddHours(-1),
            RaisedTo = raisedAt.AddHours(1)
        };

        // Act
        var result = await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        var predicate = captured!;
        predicate.Compile()(matching).Should().BeTrue();
        predicate.Compile()(otherMachine).Should().BeFalse();
        predicate.Compile()(otherCategory).Should().BeFalse();
        predicate.Compile()(otherStatus).Should().BeFalse();
        predicate.Compile()(outsideRange).Should().BeFalse();
        result.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_NoFilters_MatchesEverything()
    {
        // Arrange
        ExpressionStarter<AndonSignal>? captured = null;
        _repository.Setup(r => r.CountAsync(It.IsAny<ExpressionStarter<AndonSignal>>(), It.IsAny<CancellationToken>()))
            .Callback<ExpressionStarter<AndonSignal>, CancellationToken>((p, _) => captured = p)
            .ReturnsAsync(0);
        _repository.Setup(r => r.BrowseAsync(It.IsAny<Paginator<AndonSignal>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AndonSignal>());

        // Act
        await CreateSut().Handle(new BrowseAndonSignalsRequest(), CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        captured!.Compile()(Signal(Guid.NewGuid(), AndonSignalCategory.Other, AndonSignalStatus.Resolved, DateTime.UtcNow))
            .Should().BeTrue();
    }
}
