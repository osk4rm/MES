using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Oee.Losses;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class GetOeeLossesRequestHandlerTests
{
    private static readonly DateTime Monday = new(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IMachinesRepository> _machines = new();
    private readonly Mock<IDowntimeEventsRepository> _downtimes = new();
    private readonly Mock<IScrapEventsRepository> _scraps = new();
    private readonly Mock<IReasonCodesRepository> _reasons = new();

    private GetOeeLossesRequestHandler CreateSut() => new(
        _machines.Object, _downtimes.Object, _scraps.Object, _reasons.Object);

    private static Machine AMachine(Guid id) => new()
    {
        Id = id,
        TenantId = Guid.NewGuid(),
        Code = "WC-1",
        Name = "Work Center 1"
    };

    private static DowntimeEvent ClosedDowntime(Guid machineId, Guid reasonId, DateTime start, DateTime end) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        MachineId = machineId,
        ReasonCodeId = reasonId,
        StartedAt = start,
        EndedAt = end
    };

    private static DowntimeEvent OpenDowntime(Guid machineId, Guid reasonId, DateTime start) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        MachineId = machineId,
        ReasonCodeId = reasonId,
        StartedAt = start,
        EndedAt = null
    };

    private static ScrapEvent Scrap(Guid machineId, Guid reasonId, DateTime reportedAt, decimal quantity) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        MachineId = machineId,
        ReasonCodeId = reasonId,
        ReportedAt = reportedAt,
        Quantity = quantity
    };

    private static ReasonCode Code(Guid id, string code, string name) => new()
    {
        Id = id,
        TenantId = Guid.NewGuid(),
        Code = code,
        Name = name
    };

    private void ArrangeMachine(Guid machineId)
    {
        _machines.Setup(m => m.GetByIdAsync(machineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AMachine(machineId));
        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _scraps.Setup(s => s.ListForMachineInWindowAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _reasons.Setup(r => r.ListByIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    [Fact]
    public void Request_ImplementsTenantRequest_AndNotAnonymous()
    {
        // Assert - the losses query must stay tenant-scoped
        typeof(ITenantRequest<OeeLossesResponse>)
            .IsAssignableFrom(typeof(GetOeeLossesRequest)).Should().BeTrue();
        typeof(IAllowAnonymousRequest).IsAssignableFrom(typeof(GetOeeLossesRequest)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_HappyPath_GroupsDowntimeAndScrapByReason()
    {
        // Arrange - downtime R1 60min + R2 30min, scrap R1 10 + R3 5
        var machineId = Guid.NewGuid();
        var from = Monday.AddHours(6);
        var to = Monday.AddHours(14);
        var reason1 = Guid.NewGuid();
        var reason2 = Guid.NewGuid();
        var reason3 = Guid.NewGuid();
        ArrangeMachine(machineId);

        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                ClosedDowntime(machineId, reason1, Monday.AddHours(8), Monday.AddHours(9)),
                ClosedDowntime(machineId, reason2, Monday.AddHours(10), Monday.AddHours(10).AddMinutes(30))]);
        _scraps.Setup(s => s.ListForMachineInWindowAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Scrap(machineId, reason1, Monday.AddHours(9), 10m),
                Scrap(machineId, reason3, Monday.AddHours(11), 5m)]);
        _reasons.Setup(r => r.ListByIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Code(reason1, "DT-BREAK", "Breakdown"),
                Code(reason2, "DT-SETUP", "Setup"),
                Code(reason3, "SCR-TOL", "Tolerance over")]);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeLossesRequest(machineId, from, to), CancellationToken.None);

        // Assert
        result.MachineId.Should().Be(machineId);
        result.FromUtc.Should().Be(from);
        result.ToUtc.Should().Be(to);
        result.TotalDowntimeMinutes.Should().Be(90);
        result.TotalScrapQuantity.Should().Be(15m);

        result.DowntimePareto.Should().HaveCount(2);
        result.DowntimePareto[0].ReasonCodeId.Should().Be(reason1);
        result.DowntimePareto[0].Code.Should().Be("DT-BREAK");
        result.DowntimePareto[0].DisplayName.Should().Be("Breakdown");
        result.DowntimePareto[0].Minutes.Should().Be(60);
        result.DowntimePareto[0].Share.Should().Be(0.6667);
        result.DowntimePareto[1].ReasonCodeId.Should().Be(reason2);
        result.DowntimePareto[1].Minutes.Should().Be(30);
        result.DowntimePareto[1].Share.Should().Be(0.3333);

        result.ScrapPareto.Should().HaveCount(2);
        result.ScrapPareto[0].ReasonCodeId.Should().Be(reason1);
        result.ScrapPareto[0].Quantity.Should().Be(10m);
        result.ScrapPareto[0].Share.Should().Be(0.6667);
        result.ScrapPareto[1].ReasonCodeId.Should().Be(reason3);
        result.ScrapPareto[1].Code.Should().Be("SCR-TOL");
        result.ScrapPareto[1].DisplayName.Should().Be("Tolerance over");
        result.ScrapPareto[1].Quantity.Should().Be(5m);
        result.ScrapPareto[1].Share.Should().Be(0.3333);

        // Pareto rows always sum to the totals
        result.DowntimePareto.Select(e => e.Minutes).Sum().Should().Be(result.TotalDowntimeMinutes);
        result.DowntimePareto.Select(e => e.Share).Sum().Should().BeApproximately(1.0, 0.0001);
        result.ScrapPareto.Select(e => e.Quantity).Sum().Should().Be(result.TotalScrapQuantity);
        result.ScrapPareto.Select(e => e.Share).Sum().Should().BeApproximately(1.0, 0.0001);
    }

    [Fact]
    public async Task Handle_PartialDowntimeOverlap_CountsOverlapOnly()
    {
        // Arrange - a stop spilling outside the window counts with its overlap
        var machineId = Guid.NewGuid();
        var from = Monday.AddHours(6);
        var to = Monday.AddHours(14);
        var reason = Guid.NewGuid();
        ArrangeMachine(machineId);

        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                ClosedDowntime(machineId, reason, Monday.AddHours(5), Monday.AddHours(7)),
                ClosedDowntime(machineId, reason, Monday.AddHours(13), Monday.AddHours(15))]);
        _reasons.Setup(r => r.ListByIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Code(reason, "DT-X", "Spill")]);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeLossesRequest(machineId, from, to), CancellationToken.None);

        // Assert
        result.TotalDowntimeMinutes.Should().Be(120);
        result.DowntimePareto.Should().ContainSingle()
            .Which.Minutes.Should().Be(120);
    }

    [Fact]
    public async Task Handle_OpenDowntime_Ignored()
    {
        // Arrange - a still-open stop must not appear in the Pareto
        var machineId = Guid.NewGuid();
        var reason = Guid.NewGuid();
        ArrangeMachine(machineId);

        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                OpenDowntime(machineId, reason, Monday.AddHours(7)),
                ClosedDowntime(machineId, reason, Monday.AddHours(9), Monday.AddHours(9).AddMinutes(30))]);
        _reasons.Setup(r => r.ListByIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Code(reason, "DT-X", "Stop")]);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeLossesRequest(machineId, Monday.AddHours(6), Monday.AddHours(14)),
            CancellationToken.None);

        // Assert
        result.TotalDowntimeMinutes.Should().Be(30);
        result.DowntimePareto.Should().ContainSingle()
            .Which.Share.Should().Be(1.0);
    }

    [Fact]
    public async Task Handle_UnresolvableReason_KeepsValuesWithNullNames()
    {
        // Arrange - the dictionary has no entry for the used reason id
        // (deleted code or a foreign id): values stay, names stay null
        var machineId = Guid.NewGuid();
        var reason = Guid.NewGuid();
        ArrangeMachine(machineId);

        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([ClosedDowntime(machineId, reason, Monday.AddHours(8), Monday.AddHours(9))]);
        _scraps.Setup(s => s.ListForMachineInWindowAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Scrap(machineId, reason, Monday.AddHours(9), 4m)]);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeLossesRequest(machineId, Monday.AddHours(6), Monday.AddHours(14)),
            CancellationToken.None);

        // Assert - rows are kept so sums still hold, with no leaked names
        result.TotalDowntimeMinutes.Should().Be(60);
        result.TotalScrapQuantity.Should().Be(4m);
        var downtimeEntry = result.DowntimePareto.Should().ContainSingle().Which;
        downtimeEntry.ReasonCodeId.Should().Be(reason);
        downtimeEntry.Code.Should().BeNull();
        downtimeEntry.DisplayName.Should().BeNull();
        var scrapEntry = result.ScrapPareto.Should().ContainSingle().Which;
        scrapEntry.ReasonCodeId.Should().Be(reason);
        scrapEntry.Code.Should().BeNull();
        scrapEntry.DisplayName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ReasonLookup_ScopedToUsedIdsOnly()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        var reason = Guid.NewGuid();
        ArrangeMachine(machineId);

        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([ClosedDowntime(machineId, reason, Monday.AddHours(8), Monday.AddHours(9))]);
        _reasons.Setup(r => r.ListByIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Code(reason, "DT-X", "Stop")]);

        // Act
        await CreateSut().Handle(
            new GetOeeLossesRequest(machineId, Monday.AddHours(6), Monday.AddHours(14)),
            CancellationToken.None);

        // Assert - the dictionary is queried for exactly the grouped ids,
        // never browsed wholesale, so cross-tenant codes cannot leak in
        _reasons.Verify(
            r => r.ListByIdsAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(reason)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyWindow_ReturnsEmptyParetoAndZeroTotals()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        ArrangeMachine(machineId);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeLossesRequest(machineId, Monday.AddHours(6), Monday.AddHours(14)),
            CancellationToken.None);

        // Assert
        result.TotalDowntimeMinutes.Should().Be(0);
        result.TotalScrapQuantity.Should().Be(0m);
        result.DowntimePareto.Should().BeEmpty();
        result.ScrapPareto.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReversedWindow_ThrowsValidationException()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        ArrangeMachine(machineId);

        // Act
        var act = () => CreateSut().Handle(
            new GetOeeLossesRequest(machineId, Monday.AddHours(14), Monday.AddHours(6)),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_WindowOver93Days_ThrowsValidationException()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        ArrangeMachine(machineId);

        // Act
        var act = () => CreateSut().Handle(
            new GetOeeLossesRequest(machineId, Monday, Monday.AddDays(93).AddMinutes(1)),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_EmptyMachineId_ThrowsValidationException()
    {
        // Act
        var act = () => CreateSut().Handle(
            new GetOeeLossesRequest(Guid.Empty, Monday.AddHours(6), Monday.AddHours(14)),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _machines.Verify(
            m => m.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownMachine_ThrowsNotFoundException_WithoutTouchingData()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        _machines.Setup(m => m.GetByIdAsync(machineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Machine?)null);

        // Act
        var act = () => CreateSut().Handle(
            new GetOeeLossesRequest(machineId, Monday.AddHours(6), Monday.AddHours(14)),
            CancellationToken.None);

        // Assert - cross-tenant machines are invisible through the tenant
        // filter, so they surface here as unknown ids (404, never data)
        await act.Should().ThrowAsync<NotFoundException>();
        _downtimes.Verify(
            d => d.ListOverlappingAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _scraps.Verify(
            s => s.ListForMachineInWindowAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _reasons.Verify(
            r => r.ListByIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
