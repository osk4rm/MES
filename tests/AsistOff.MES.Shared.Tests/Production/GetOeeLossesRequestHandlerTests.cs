using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
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

    private static ReasonCode AReason(Guid id, string code, string name) => new()
    {
        Id = id,
        TenantId = Guid.NewGuid(),
        Code = code,
        Name = name,
        Category = ReasonCodeCategory.Downtime,
        IsActive = true,
        SortIndex = 0
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

    private static ScrapEvent AScrap(Guid machineId, Guid reasonId, decimal quantity, DateTime reportedAt) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        MachineId = machineId,
        ReasonCodeId = reasonId,
        Quantity = quantity,
        ReportedAt = reportedAt
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
    }

    private void ArrangeReasons(params ReasonCode[] reasons)
    {
        foreach (var reason in reasons)
        {
            var captured = reason;
            _reasons.Setup(r => r.GetByIdAsync(captured.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(captured);
        }
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
    public async Task Handle_HappyPath_ParetoSumsAndShares()
    {
        // Arrange - downtime: A 60min + B 30min; scrap: A 10 + 5, B 5
        var machineId = Guid.NewGuid();
        var reasonA = Guid.NewGuid();
        var reasonB = Guid.NewGuid();
        var from = Monday.AddHours(6);
        var to = Monday.AddHours(14);
        ArrangeMachine(machineId);
        ArrangeReasons(
            AReason(reasonA, "DT-A", "Breakdown A"),
            AReason(reasonB, "DT-B", "Breakdown B"));

        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                ClosedDowntime(machineId, reasonA, Monday.AddHours(8), Monday.AddHours(9)),
                ClosedDowntime(machineId, reasonB, Monday.AddHours(10), Monday.AddHours(10).AddMinutes(30))]);
        _scraps.Setup(s => s.ListForMachineInWindowAsync(
                machineId, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                AScrap(machineId, reasonA, 10m, Monday.AddHours(9)),
                AScrap(machineId, reasonA, 5m, Monday.AddHours(10)),
                AScrap(machineId, reasonB, 5m, Monday.AddHours(11))]);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeLossesRequest(machineId, from, to), CancellationToken.None);

        // Assert
        result.MachineId.Should().Be(machineId);
        result.TotalDowntimeMinutes.Should().Be(90);
        result.TotalScrapQuantity.Should().Be(20m);
        result.DowntimePareto.Should().HaveCount(2);
        result.DowntimePareto[0].ReasonCodeId.Should().Be(reasonA);
        result.DowntimePareto[0].Code.Should().Be("DT-A");
        result.DowntimePareto[0].Name.Should().Be("Breakdown A");
        result.DowntimePareto[0].Minutes.Should().Be(60);
        result.DowntimePareto[0].Share.Should().Be(0.6667);
        result.DowntimePareto[1].Minutes.Should().Be(30);
        result.DowntimePareto[1].Share.Should().Be(0.3333);
        result.DowntimePareto.Sum(e => e.Minutes).Should().Be(result.TotalDowntimeMinutes);
        result.DowntimePareto.Sum(e => e.Share).Should().BeApproximately(1.0, 0.0001);
        result.ScrapPareto.Should().HaveCount(2);
        result.ScrapPareto[0].ReasonCodeId.Should().Be(reasonA);
        result.ScrapPareto[0].Quantity.Should().Be(15m);
        result.ScrapPareto[0].Share.Should().Be(0.75);
        result.ScrapPareto[1].Quantity.Should().Be(5m);
        result.ScrapPareto[1].Share.Should().Be(0.25);
        result.ScrapPareto.Sum(e => e.Quantity).Should().Be(result.TotalScrapQuantity);
        result.ScrapPareto.Sum(e => e.Share).Should().BeApproximately(1.0, 0.0001);
    }

    [Fact]
    public async Task Handle_OpenDowntime_Ignored()
    {
        // Arrange - a still-open stop must not appear in the Pareto
        var machineId = Guid.NewGuid();
        var reasonId = Guid.NewGuid();
        ArrangeMachine(machineId);
        ArrangeReasons(AReason(reasonId, "DT-A", "Breakdown A"));

        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                OpenDowntime(machineId, reasonId, Monday.AddHours(7)),
                ClosedDowntime(machineId, reasonId, Monday.AddHours(9), Monday.AddHours(9).AddMinutes(30))]);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeLossesRequest(machineId, Monday.AddHours(6), Monday.AddHours(14)),
            CancellationToken.None);

        // Assert
        result.TotalDowntimeMinutes.Should().Be(30);
        result.DowntimePareto.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_PartialDowntimeOverlap_CountsOverlapOnly()
    {
        // Arrange - stops spilling outside the window count only with overlap
        var machineId = Guid.NewGuid();
        var reasonId = Guid.NewGuid();
        ArrangeMachine(machineId);
        ArrangeReasons(AReason(reasonId, "DT-A", "Breakdown A"));

        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                ClosedDowntime(machineId, reasonId, Monday.AddHours(5), Monday.AddHours(7)),
                ClosedDowntime(machineId, reasonId, Monday.AddHours(13), Monday.AddHours(15))]);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeLossesRequest(machineId, Monday.AddHours(6), Monday.AddHours(14)),
            CancellationToken.None);

        // Assert
        result.TotalDowntimeMinutes.Should().Be(120);
        result.DowntimePareto.Should().ContainSingle();
        result.DowntimePareto[0].Minutes.Should().Be(120);
        result.DowntimePareto[0].Share.Should().Be(1.0);
    }

    [Fact]
    public async Task Handle_NoLosses_ReturnsEmptyPareto()
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
    public async Task Handle_UnknownReason_FallsBackToUnknown_WithoutLeakingForeignCode()
    {
        // Arrange - the downtime references a reason invisible to the caller
        // tenant (deleted or cross-tenant): the lookup returns null
        var machineId = Guid.NewGuid();
        var foreignReasonId = Guid.NewGuid();
        ArrangeMachine(machineId);

        _downtimes.Setup(d => d.ListOverlappingAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([ClosedDowntime(machineId, foreignReasonId, Monday.AddHours(8), Monday.AddHours(9))]);
        _reasons.Setup(r => r.GetByIdAsync(foreignReasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReasonCode?)null);

        // Act
        var result = await CreateSut().Handle(
            new GetOeeLossesRequest(machineId, Monday.AddHours(6), Monday.AddHours(14)),
            CancellationToken.None);

        // Assert - minutes are preserved but no foreign code leaks
        result.TotalDowntimeMinutes.Should().Be(60);
        result.DowntimePareto.Should().ContainSingle();
        result.DowntimePareto[0].ReasonCodeId.Should().Be(foreignReasonId);
        result.DowntimePareto[0].Code.Should().Be("Unknown");
        result.DowntimePareto[0].Name.Should().Be("Unknown");
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
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
