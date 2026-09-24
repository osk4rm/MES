using AsistOff.MES.Production.Application.Features.TelemetryReadings;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Production.Infrastructure.Telemetry;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;
using TelemetryReadingResponse = AsistOff.MES.Production.Application.Features.TelemetryReadings.TelemetryReadingResponse;

namespace AsistOff.MES.Shared.Tests.Production;

public class SimulatorBackedOpcUaReaderTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    private static MachineTelemetryTag Tag(Guid id, Guid machineId, TelemetryDataType dataType, bool isEnabled = true) => new()
    {
        Id = id,
        TenantId = Guid.NewGuid(),
        MachineId = machineId,
        NodeId = $"ns=2;s={id:N}",
        DisplayName = "Sensor",
        DataType = dataType,
        IsEnabled = isEnabled
    };

    [Fact]
    public async Task PollAsync_WritesReadingsOnlyForEnabledTagsOfConnectionMachine()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        var otherMachineId = Guid.NewGuid();
        var wanted = Tag(Guid.NewGuid(), machineId, TelemetryDataType.Double, isEnabled: true);
        var disabledSameMachine = Tag(Guid.NewGuid(), machineId, TelemetryDataType.Double, isEnabled: false);
        var enabledOtherMachine = Tag(Guid.NewGuid(), otherMachineId, TelemetryDataType.Double, isEnabled: true);

        var tags = new Mock<IMachineTelemetryTagsRepository>();
        tags.Setup(t => t.ListEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { wanted, enabledOtherMachine });

        var readings = new Mock<ITelemetryReadingsRepository>();
        readings.Setup(r => r.GetLatestAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TelemetryReading?)null);

        var ingestion = new Mock<ITelemetryIngestionService>();
        ingestion.Setup(i => i.IngestAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<double?>(), It.IsAny<string?>(),
                It.IsAny<TelemetryQuality>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid tagId, DateTime readAt, double? doubleValue, string? stringValue,
                TelemetryQuality quality, Guid tenantId, CancellationToken _) =>
                new TelemetryReadingResponse(
                    Guid.NewGuid(), tagId, machineId, readAt, doubleValue, stringValue, quality));

        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        var reader = new SimulatorBackedOpcUaReader(tags.Object, readings.Object, ingestion.Object, clock.Object);
        var connection = new OpcUaConnection
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            MachineId = machineId,
            EndpointUrl = "opc.tcp://plc-1:4840"
        };

        // Act
        var written = await reader.PollAsync(connection, _tenantId);

        // Assert
        written.Should().Be(1);
        ingestion.Verify(i => i.IngestAsync(
            wanted.Id, _now, It.IsAny<double?>(), null, TelemetryQuality.Good, _tenantId,
            It.IsAny<CancellationToken>()), Times.Once);
        ingestion.Verify(i => i.IngestAsync(
            disabledSameMachine.Id, It.IsAny<DateTime>(), It.IsAny<double?>(), It.IsAny<string?>(),
            It.IsAny<TelemetryQuality>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        ingestion.Verify(i => i.IngestAsync(
            enabledOtherMachine.Id, It.IsAny<DateTime>(), It.IsAny<double?>(), It.IsAny<string?>(),
            It.IsAny<TelemetryQuality>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
