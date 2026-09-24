using AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Status;
using AsistOff.MES.Production.Application.Telemetry;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class GetTelemetryStatusRequestHandlerTests
{
    private readonly Mock<IMachineTelemetryTagsRepository> _tags = new();
    private readonly Mock<ITelemetryReadingsRepository> _readings = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IOptionsMonitor<TelemetryOptions>> _options = new();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    private readonly Guid _recentTagId = Guid.NewGuid();
    private readonly Guid _staleTagId = Guid.NewGuid();
    private readonly Guid _neverTagId = Guid.NewGuid();

    public GetTelemetryStatusRequestHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _options.SetupGet(o => o.CurrentValue).Returns(new TelemetryOptions
        {
            SimulatorEnabled = true,
            SimulatorIntervalSeconds = 30
        });

        _tags.Setup(t => t.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Tag(_recentTagId, "ns=2;s=recent"),
                Tag(_staleTagId, "ns=2;s=stale"),
                Tag(_neverTagId, "ns=2;s=never")
            });

        _readings.Setup(r => r.GetLatestAsync(_recentTagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelemetryReading { TagId = _recentTagId, ReadAt = _now.AddSeconds(-10), DoubleValue = 21.5 });
        _readings.Setup(r => r.GetLatestAsync(_staleTagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelemetryReading { TagId = _staleTagId, ReadAt = _now.AddMinutes(-5), DoubleValue = 18.0 });
        _readings.Setup(r => r.GetLatestAsync(_neverTagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TelemetryReading?)null);

        _readings.Setup(r => r.CountSinceAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid tagId, DateTime since, CancellationToken _) =>
                tagId == _neverTagId ? 0 : 12);
    }

    private static MachineTelemetryTag Tag(Guid id, string nodeId) => new()
    {
        Id = id,
        TenantId = Guid.NewGuid(),
        MachineId = Guid.NewGuid(),
        NodeId = nodeId,
        DisplayName = nodeId,
        DataType = TelemetryDataType.Double,
        IsEnabled = true
    };

    private GetTelemetryStatusRequestHandler CreateSut() =>
        new(_tags.Object, _readings.Object, _clock.Object, _options.Object);

    [Fact]
    public async Task Handle_ReturnsOneEntryPerTag_WithSimulatorFlag()
    {
        // Act
        var result = await CreateSut().Handle(new GetTelemetryStatusRequest(), CancellationToken.None);

        // Assert
        result.SimulatorEnabled.Should().BeTrue();
        result.SimulatorIntervalSeconds.Should().Be(30);
        result.Tags.Should().HaveCount(3);
        result.Tags.Select(t => t.TagId).Should().BeEquivalentTo(new[] { _recentTagId, _staleTagId, _neverTagId });
    }

    [Fact]
    public async Task Handle_ComputesStale_AndNeverRead()
    {
        // Act
        var result = await CreateSut().Handle(new GetTelemetryStatusRequest(), CancellationToken.None);

        // Assert - 2x30s = 60s threshold: 10s ago is recent, 5min ago is stale
        var recent = result.Tags.Single(t => t.TagId == _recentTagId);
        recent.Stale.Should().BeFalse();
        recent.LastReadAt.Should().Be(_now.AddSeconds(-10));
        recent.ReadingsLastHour.Should().Be(12);

        var stale = result.Tags.Single(t => t.TagId == _staleTagId);
        stale.Stale.Should().BeTrue();
        stale.LastReadAt.Should().Be(_now.AddMinutes(-5));

        // Never-reported tags carry a null LastReadAt and are not stale
        var never = result.Tags.Single(t => t.TagId == _neverTagId);
        never.LastReadAt.Should().BeNull();
        never.Stale.Should().BeFalse();
        never.ReadingsLastHour.Should().Be(0);
    }
}
