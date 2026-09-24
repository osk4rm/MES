using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Production.Application.Features.OpcUaConnections;
using AsistOff.MES.Production.Application.Features.OpcUaConnections.Status;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class GetOpcUaConnectionStatusRequestHandlerTests
{
    private readonly Mock<IOpcUaConnectionsRepository> _connections = new();
    private readonly Mock<IMachineTelemetryTagsRepository> _tags = new();
    private readonly Mock<ITelemetryReadingsRepository> _readings = new();
    private readonly Mock<IMachinesRepository> _machines = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    private readonly Guid _machineA = Guid.NewGuid();
    private readonly Guid _machineB = Guid.NewGuid();

    private readonly Guid _liveId = Guid.NewGuid();
    private readonly Guid _staleId = Guid.NewGuid();
    private readonly Guid _neverId = Guid.NewGuid();
    private readonly Guid _disabledId = Guid.NewGuid();

    private readonly Guid _recentTagId = Guid.NewGuid();
    private readonly Guid _staleTagId = Guid.NewGuid();
    private readonly Guid _neverTagId = Guid.NewGuid();
    private readonly Guid _otherMachineTagId = Guid.NewGuid();

    public GetOpcUaConnectionStatusRequestHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);

        // Poll interval 30s everywhere, so the liveness threshold is 60s.
        _connections.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Connection(_staleId, _machineA, "opc.tcp://plc-a:4840/second", true, _now.AddMinutes(-5)),
                Connection(_liveId, _machineA, "opc.tcp://plc-a:4840", true, _now.AddSeconds(-10)),
                Connection(_neverId, _machineB, "opc.tcp://plc-b:4840", true, null),
                Connection(_disabledId, _machineB, "opc.tcp://plc-b:4840/disabled", false, _now.AddSeconds(-10))
            });

        _tags.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Tag(_recentTagId, _machineA),
                Tag(_staleTagId, _machineA),
                Tag(_neverTagId, _machineA),
                Tag(_otherMachineTagId, _machineB)
            });

        _readings.Setup(r => r.GetLatestAsync(_recentTagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelemetryReading { TagId = _recentTagId, MachineId = _machineA, ReadAt = _now.AddSeconds(-10), DoubleValue = 21.5 });
        _readings.Setup(r => r.GetLatestAsync(_staleTagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelemetryReading { TagId = _staleTagId, MachineId = _machineA, ReadAt = _now.AddMinutes(-5), DoubleValue = 18.0 });
        _readings.Setup(r => r.GetLatestAsync(_neverTagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TelemetryReading?)null);
        _readings.Setup(r => r.GetLatestAsync(_otherMachineTagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelemetryReading { TagId = _otherMachineTagId, MachineId = _machineB, ReadAt = _now.AddSeconds(-5), DoubleValue = 7.0 });
    }

    private static OpcUaConnection Connection(Guid id, Guid machineId, string endpointUrl, bool isEnabled, DateTime? lastSeen) => new()
    {
        Id = id,
        TenantId = Guid.NewGuid(),
        MachineId = machineId,
        EndpointUrl = endpointUrl,
        PollIntervalSeconds = 30,
        IsEnabled = isEnabled,
        LastSeenAtUtc = lastSeen
    };

    private static MachineTelemetryTag Tag(Guid id, Guid machineId) => new()
    {
        Id = id,
        TenantId = Guid.NewGuid(),
        MachineId = machineId,
        NodeId = $"ns=2;s={id:N}",
        DisplayName = $"tag-{id:N}",
        DataType = TelemetryDataType.Double,
        PollIntervalSeconds = 30,
        IsEnabled = true
    };

    private GetOpcUaConnectionStatusRequestHandler CreateSut() => new(
        _connections.Object, _tags.Object, _readings.Object, _machines.Object, _clock.Object);

    [Fact]
    public async Task Handle_ReturnsOneEntryPerConnection_InStableOrder()
    {
        // Act
        var result = await CreateSut().Handle(new GetOpcUaConnectionStatusRequest(), CancellationToken.None);

        // Assert - stable (machine, endpoint) order regardless of seed order
        result.Connections.Should().HaveCount(4);
        result.TotalCount.Should().Be(4);
        result.Connections.Select(e => e.EndpointUrl).Should().ContainInOrder(
            "opc.tcp://plc-a:4840",
            "opc.tcp://plc-a:4840/second",
            "opc.tcp://plc-b:4840",
            "opc.tcp://plc-b:4840/disabled");
    }

    [Fact]
    public async Task Handle_ComputesIsLive_LiveStaleAndNeverSeen()
    {
        // Act
        var result = await CreateSut().Handle(new GetOpcUaConnectionStatusRequest(), CancellationToken.None);

        // Assert - 2x30s = 60s threshold: 10s ago is live, 5min ago is stale
        var live = result.Connections.Single(e => e.ConnectionId == _liveId);
        live.IsLive.Should().BeTrue();
        live.IsEnabled.Should().BeTrue();

        var stale = result.Connections.Single(e => e.ConnectionId == _staleId);
        stale.IsLive.Should().BeFalse();

        // Never-polled connections surface as stale (never seen), not live
        var never = result.Connections.Single(e => e.ConnectionId == _neverId);
        never.IsLive.Should().BeFalse();
        never.LastSeenAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Handle_DisabledConnection_IsNotLive_AndExcludedFromLiveTotals()
    {
        // Act
        var result = await CreateSut().Handle(new GetOpcUaConnectionStatusRequest(), CancellationToken.None);

        // Assert - disabled is not applicable: never live even with a fresh poll
        var disabled = result.Connections.Single(e => e.ConnectionId == _disabledId);
        disabled.IsEnabled.Should().BeFalse();
        disabled.IsLive.Should().BeFalse();

        result.LiveCount.Should().Be(1);
        result.StaleCount.Should().Be(2);
        result.DisabledCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ComputesTagCounts_FromReadingRecency()
    {
        // Act
        var result = await CreateSut().Handle(new GetOpcUaConnectionStatusRequest(), CancellationToken.None);

        // Assert - machine A: 3 tags, 1 reporting (10s), 2 stale (5min + never)
        foreach (var entry in result.Connections.Where(e => e.MachineId == _machineA))
        {
            entry.TotalTags.Should().Be(3);
            entry.ReportingTags.Should().Be(1);
            entry.StaleTags.Should().Be(2);
        }

        // Machine B: 1 tag with a fresh reading, fully reporting
        foreach (var entry in result.Connections.Where(e => e.MachineId == _machineB))
        {
            entry.TotalTags.Should().Be(1);
            entry.ReportingTags.Should().Be(1);
            entry.StaleTags.Should().Be(0);
        }
    }

    [Fact]
    public async Task Handle_MachineFilter_ReturnsOnlyMatchingConnections()
    {
        // Arrange
        _machines.Setup(m => m.GetByIdAsync(_machineA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Machine { Id = _machineA, TenantId = Guid.NewGuid(), Code = "WC-A", Name = "Work Center A" });

        // Act
        var result = await CreateSut().Handle(
            new GetOpcUaConnectionStatusRequest { MachineId = _machineA }, CancellationToken.None);

        // Assert
        result.Connections.Should().HaveCount(2);
        result.Connections.Select(e => e.MachineId).Should().AllBeEquivalentTo(_machineA);
        result.TotalCount.Should().Be(2);
        result.LiveCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_UnknownMachineId_ThrowsNotFoundException()
    {
        // Arrange
        _machines.Setup(m => m.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Machine?)null);

        // Act
        var act = () => CreateSut().Handle(
            new GetOpcUaConnectionStatusRequest { MachineId = Guid.NewGuid() }, CancellationToken.None);

        // Assert - unknown and cross-tenant ids are both invisible, hence 404
        await act.Should().ThrowAsync<NotFoundException>();
    }
}

public class OpcUaConnectionHealthTests
{
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    private static OpcUaConnection Connection(bool isEnabled, int pollIntervalSeconds, DateTime? lastSeen) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        MachineId = Guid.NewGuid(),
        EndpointUrl = "opc.tcp://plc-1:4840",
        PollIntervalSeconds = pollIntervalSeconds,
        IsEnabled = isEnabled,
        LastSeenAtUtc = lastSeen
    };

    [Theory]
    [InlineData(-60, true)] // exactly at the 2x30s threshold: still live
    [InlineData(-10, true)]
    [InlineData(-61, false)] // one second past the threshold: stale
    [InlineData(-300, false)]
    public void IsLive_EnabledConnection_AppliesTwoTimesPollIntervalRule(int offsetSeconds, bool expected)
    {
        // Arrange
        var connection = Connection(true, 30, _now.AddSeconds(offsetSeconds));

        // Act
        var live = OpcUaConnectionHealth.IsLive(connection, _now);

        // Assert
        live.Should().Be(expected);
    }

    [Fact]
    public void IsLive_NeverSeenConnection_IsStale()
    {
        // Arrange
        var connection = Connection(true, 30, null);

        // Act
        var live = OpcUaConnectionHealth.IsLive(connection, _now);

        // Assert
        live.Should().BeFalse();
    }

    [Fact]
    public void IsLive_DisabledConnection_IsNeverLive()
    {
        // Arrange
        var connection = Connection(false, 30, _now);

        // Act
        var live = OpcUaConnectionHealth.IsLive(connection, _now);

        // Assert
        live.Should().BeFalse();
    }
}
