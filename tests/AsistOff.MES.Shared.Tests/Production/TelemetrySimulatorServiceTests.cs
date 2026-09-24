using AsistOff.MES.Multitenancy.Repositories;
using AsistOff.MES.Production.Application.Features.TelemetryReadings;
using AsistOff.MES.Production.Application.Telemetry;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Production.Infrastructure.Telemetry;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class TelemetrySimulatorServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
    private readonly ILogger<TelemetrySimulatorService> _logger = NullLogger<TelemetrySimulatorService>.Instance;

    private static Mock<IOptionsMonitor<TelemetryOptions>> Options(bool enabled, int intervalSeconds = 30)
    {
        var options = new Mock<IOptionsMonitor<TelemetryOptions>>();
        options.SetupGet(o => o.CurrentValue).Returns(new TelemetryOptions
        {
            SimulatorEnabled = enabled,
            SimulatorIntervalSeconds = intervalSeconds
        });
        return options;
    }

    private static MachineTelemetryTag Tag(Guid id, TelemetryDataType dataType, bool isEnabled = true) => new()
    {
        Id = id,
        TenantId = Guid.NewGuid(),
        MachineId = Guid.NewGuid(),
        NodeId = $"ns=2;s={id:N}",
        DisplayName = "Sensor",
        DataType = dataType,
        IsEnabled = isEnabled
    };

    private static void SetupEmptyReadings(Mock<ITelemetryReadingsRepository> readings)
    {
        readings.Setup(r => r.GetLatestAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TelemetryReading?)null);
    }

    private static void SetupIngestion(Mock<ITelemetryIngestionService> ingestion)
    {
        ingestion.Setup(i => i.IngestAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<double?>(), It.IsAny<string?>(),
                It.IsAny<TelemetryQuality>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid tagId, DateTime readAt, double? doubleValue, string? stringValue,
                TelemetryQuality quality, Guid tenantId, CancellationToken _) =>
                new TelemetryReadingResponse(
                    Guid.NewGuid(), tagId, Guid.NewGuid(), readAt, doubleValue, stringValue, quality));
    }

    private TelemetrySimulatorService CreateSut(
        Mock<IServiceScopeFactory> scopeFactory, Mock<IOptionsMonitor<TelemetryOptions>> options)
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);
        return new TelemetrySimulatorService(scopeFactory.Object, options.Object, clock.Object, _logger);
    }

    [Fact]
    public async Task PollOnceAsync_SimulatorDisabled_WritesNothing()
    {
        // Arrange
        var scopeFactory = new Mock<IServiceScopeFactory>();
        var sut = CreateSut(scopeFactory, Options(enabled: false));

        // Act
        var written = await sut.PollOnceAsync();

        // Assert
        written.Should().Be(0);
        scopeFactory.Verify(f => f.CreateScope(), Times.Never);
    }

    [Fact]
    public async Task PollOnceAsync_Enabled_PollsEachActiveTenant()
    {
        // Arrange
        var otherTenantId = Guid.NewGuid();
        var tag = Tag(Guid.NewGuid(), TelemetryDataType.Double);

        var tenants = new Mock<ITenantRepository>();
        tenants.Setup(t => t.ListActiveIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _tenantId, otherTenantId });

        var tags = new Mock<IMachineTelemetryTagsRepository>();
        tags.Setup(t => t.ListEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { tag });

        var readings = new Mock<ITelemetryReadingsRepository>();
        SetupEmptyReadings(readings);

        var ingestion = new Mock<ITelemetryIngestionService>();
        SetupIngestion(ingestion);

        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(ITenantRepository))).Returns(tenants.Object);
        provider.Setup(p => p.GetService(typeof(IMachineTelemetryTagsRepository))).Returns(tags.Object);
        provider.Setup(p => p.GetService(typeof(ITelemetryReadingsRepository))).Returns(readings.Object);
        provider.Setup(p => p.GetService(typeof(ITelemetryIngestionService))).Returns(ingestion.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(provider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        var sut = CreateSut(scopeFactory, Options(enabled: true));

        // Act
        var written = await sut.PollOnceAsync();

        // Assert
        written.Should().Be(2);
        ingestion.Verify(i => i.IngestAsync(
            tag.Id, _now, It.IsAny<double?>(), null, TelemetryQuality.Good, _tenantId,
            It.IsAny<CancellationToken>()), Times.Once);
        ingestion.Verify(i => i.IngestAsync(
            tag.Id, _now, It.IsAny<double?>(), null, TelemetryQuality.Good, otherTenantId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PollTenantAsync_DisabledTags_Skipped()
    {
        // Arrange
        var enabled = Tag(Guid.NewGuid(), TelemetryDataType.Double, isEnabled: true);
        var disabled = Tag(Guid.NewGuid(), TelemetryDataType.Double, isEnabled: false);

        var tags = new Mock<IMachineTelemetryTagsRepository>();
        tags.Setup(t => t.ListEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { enabled, disabled });

        var readings = new Mock<ITelemetryReadingsRepository>();
        SetupEmptyReadings(readings);

        var ingestion = new Mock<ITelemetryIngestionService>();
        SetupIngestion(ingestion);

        // Act
        var written = await TelemetrySimulatorService.PollTenantAsync(
            tags.Object, readings.Object, ingestion.Object, _tenantId, _now, new Random(42), _logger);

        // Assert
        written.Should().Be(1);
        ingestion.Verify(i => i.IngestAsync(
            enabled.Id, It.IsAny<DateTime>(), It.IsAny<double?>(), It.IsAny<string?>(),
            It.IsAny<TelemetryQuality>(), _tenantId, It.IsAny<CancellationToken>()), Times.Once);
        ingestion.Verify(i => i.IngestAsync(
            disabled.Id, It.IsAny<DateTime>(), It.IsAny<double?>(), It.IsAny<string?>(),
            It.IsAny<TelemetryQuality>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PollTenantAsync_TagFailure_DoesNotAbortBatch()
    {
        // Arrange
        var failing = Tag(Guid.NewGuid(), TelemetryDataType.Double);
        var healthy = Tag(Guid.NewGuid(), TelemetryDataType.Double);

        var tags = new Mock<IMachineTelemetryTagsRepository>();
        tags.Setup(t => t.ListEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { failing, healthy });

        var readings = new Mock<ITelemetryReadingsRepository>();
        SetupEmptyReadings(readings);

        var ingestion = new Mock<ITelemetryIngestionService>();
        SetupIngestion(ingestion);
        ingestion.Setup(i => i.IngestAsync(
                failing.Id, It.IsAny<DateTime>(), It.IsAny<double?>(), It.IsAny<string?>(),
                It.IsAny<TelemetryQuality>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        var written = await TelemetrySimulatorService.PollTenantAsync(
            tags.Object, readings.Object, ingestion.Object, _tenantId, _now, new Random(42), _logger);

        // Assert
        written.Should().Be(1);
        ingestion.Verify(i => i.IngestAsync(
            healthy.Id, It.IsAny<DateTime>(), It.IsAny<double?>(), It.IsAny<string?>(),
            It.IsAny<TelemetryQuality>(), _tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void BuildNextValue_DoubleTag_WalksAroundPrevious()
    {
        // Arrange
        var tag = Tag(Guid.NewGuid(), TelemetryDataType.Double);
        var previous = new TelemetryReading { DoubleValue = 20.0 };

        // Act
        var (doubleValue, stringValue) = TelemetrySimulatorService.BuildNextValue(tag, previous, new Random(42));

        // Assert
        stringValue.Should().BeNull();
        doubleValue.Should().NotBeNull();
        doubleValue!.Value.Should().BeInRange(19.0, 21.0);
    }

    [Fact]
    public void BuildNextValue_IntegerTag_StaysIntegral()
    {
        // Arrange
        var tag = Tag(Guid.NewGuid(), TelemetryDataType.Integer);
        var previous = new TelemetryReading { DoubleValue = 7.0 };

        // Act
        var (doubleValue, stringValue) = TelemetrySimulatorService.BuildNextValue(tag, previous, new Random(42));

        // Assert
        stringValue.Should().BeNull();
        doubleValue.Should().Be(7.0 + Math.Round(doubleValue!.Value - 7.0));
        doubleValue!.Value.Should().BeInRange(6.0, 8.0);
    }

    [Fact]
    public void BuildNextValue_BooleanTag_IsZeroOrOne()
    {
        // Arrange
        var tag = Tag(Guid.NewGuid(), TelemetryDataType.Boolean);
        var previous = new TelemetryReading { DoubleValue = 1.0 };

        // Act
        var (doubleValue, stringValue) = TelemetrySimulatorService.BuildNextValue(tag, previous, new Random(42));

        // Assert
        stringValue.Should().BeNull();
        doubleValue.Should().BeOneOf(0.0, 1.0);
    }

    [Fact]
    public void BuildNextValue_StringTag_RepeatsHeartbeat()
    {
        // Arrange
        var tag = Tag(Guid.NewGuid(), TelemetryDataType.String);

        // Act
        var withoutPrevious = TelemetrySimulatorService.BuildNextValue(tag, null, new Random(42));
        var withPrevious = TelemetrySimulatorService.BuildNextValue(
            tag, new TelemetryReading { StringValue = "running" }, new Random(42));

        // Assert
        withoutPrevious.DoubleValue.Should().BeNull();
        withoutPrevious.StringValue.Should().Be("sim-ok");
        withPrevious.DoubleValue.Should().BeNull();
        withPrevious.StringValue.Should().Be("running");
    }
}
