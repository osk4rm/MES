using System.Diagnostics.Metrics;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.DowntimeEvents.Start;
using AsistOff.MES.Production.Application.Features.Oee.Snapshot;
using AsistOff.MES.Production.Application.Features.ProductionConfirmations.Create;
using AsistOff.MES.Production.Application.Features.ScrapEvents.Create;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Observability;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

/// <summary>
/// Proves the instrumented handlers actually emit the business meters (issue
/// #253): confirmation increments the counter and duration histogram, scrap
/// and downtime carry the resolved reason label (<c>unknown</c> when the code
/// is unresolvable), and the OEE snapshot records its duration. Measurements
/// are observed with an in-process <c>MeterListener</c> filtered by unique
/// ids, so parallel suites cannot cross-contaminate assertions.
/// </summary>
public class MesMetersHandlerTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CreateConfirmation_EmitsCounterAndDuration_WithWorkCenterLabel()
    {
        // Arrange
        using var capture = new MeterCapture();
        var machineId = Guid.NewGuid();
        var order = new ProductionOrder
        {
            Id = Guid.NewGuid(),
            Code = "PO-METER",
            ProductId = Guid.NewGuid(),
            RecipeId = Guid.NewGuid(),
            RecipeVersionId = Guid.NewGuid(),
            PlannedQuantity = 100m,
            Status = ProductionOrderStatus.Released,
            ReleasedAt = _now.AddHours(-2)
        };
        var orders = MockOrders(order);
        var tenant = MockTenant();
        var unitOfWork = new Mock<IProductionUnitOfWork>();
        unitOfWork.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> op, CancellationToken _) => op());
        var handler = new CreateProductionConfirmationRequestHandler(
            Mock.Of<IProductionConfirmationsRepository>(),
            orders.Object,
            MockChildEntities(),
            Mock.Of<IStockMovementsRepository>(),
            Mock.Of<ILotsRepository>(),
            Mock.Of<ILotGenealogyEdgesRepository>(),
            unitOfWork.Object,
            MockGuids(),
            MockClock(),
            tenant.Object);

        // Act
        await handler.Handle(
            new CreateProductionConfirmationRequest(
                order.Id, machineId, null, _now.AddHours(-1), 10m, 0m, null),
            CancellationToken.None);

        // Assert
        capture.Single(MesMeters.ConfirmationsCounterName, machineId).Value.Should().Be(1);
        capture.Single(MesMeters.ConfirmationsCounterName, machineId).Tag(MesMeters.WorkCenterLabel)
            .Should().Be(machineId.ToString("D"));
        capture.Single(MesMeters.ConfirmationsCounterName, machineId).Tag(MesMeters.TenantLabel)
            .Should().Be(_tenantId.ToString("D"));
        capture.Single(MesMeters.ConfirmationHistogramName, machineId).Tag(MesMeters.WorkCenterLabel)
            .Should().Be(machineId.ToString("D"));
    }

    [Fact]
    public async Task CreateScrap_EmitsCounter_WithResolvedReasonLabel()
    {
        // Arrange
        using var capture = new MeterCapture();
        var machineId = Guid.NewGuid();
        var reasonId = Guid.NewGuid();
        var reasonCodes = new Mock<IReasonCodesRepository>();
        reasonCodes.Setup(r => r.GetByIdAsync(reasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReasonCode
            {
                Id = reasonId,
                TenantId = _tenantId,
                Code = "SCRAP-QA-REJECT",
                Name = "QA reject"
            });
        var handler = new CreateScrapEventRequestHandler(
            Mock.Of<IScrapEventsRepository>(),
            Mock.Of<IProductionOrdersRepository>(),
            reasonCodes.Object,
            MockGuids(),
            MockClock(),
            MockTenant().Object);

        // Act
        await handler.Handle(
            new CreateScrapEventRequest(
                machineId, reasonId, 5m, _now.AddHours(-1), null, null, null),
            CancellationToken.None);

        // Assert
        capture.Single(MesMeters.ScrapCounterName, machineId).Value.Should().Be(1);
        capture.Single(MesMeters.ScrapCounterName, machineId).Tag(MesMeters.WorkCenterLabel)
            .Should().Be(machineId.ToString("D"));
        capture.Single(MesMeters.ScrapCounterName, machineId).Tag(MesMeters.ReasonCodeLabel)
            .Should().Be("SCRAP-QA-REJECT");
    }

    [Fact]
    public async Task CreateScrap_UnresolvableReason_EmitsUnknownLabel()
    {
        // Arrange — the tenant-filtered lookup hides deleted and cross-tenant
        // codes, so the meter must fall back to the bounded placeholder.
        using var capture = new MeterCapture();
        var machineId = Guid.NewGuid();
        var handler = new CreateScrapEventRequestHandler(
            Mock.Of<IScrapEventsRepository>(),
            Mock.Of<IProductionOrdersRepository>(),
            Mock.Of<IReasonCodesRepository>(),
            MockGuids(),
            MockClock(),
            MockTenant().Object);

        // Act
        await handler.Handle(
            new CreateScrapEventRequest(
                machineId, Guid.NewGuid(), 5m, _now.AddHours(-1), null, null, null),
            CancellationToken.None);

        // Assert
        capture.Single(MesMeters.ScrapCounterName, machineId).Tag(MesMeters.ReasonCodeLabel)
            .Should().Be(MesMeters.UnknownReasonCode);
    }

    [Fact]
    public async Task StartDowntime_EmitsCounter_WithResolvedReasonLabel()
    {
        // Arrange
        using var capture = new MeterCapture();
        var machineId = Guid.NewGuid();
        var reasonId = Guid.NewGuid();
        var downtimes = new Mock<IDowntimeEventsRepository>();
        downtimes.Setup(r => r.HasOpenEventAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var reasonCodes = new Mock<IReasonCodesRepository>();
        reasonCodes.Setup(r => r.GetByIdAsync(reasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReasonCode
            {
                Id = reasonId,
                TenantId = _tenantId,
                Code = "DT-MAINT-BREAKDOWN",
                Name = "Breakdown"
            });
        var handler = new StartDowntimeEventRequestHandler(
            downtimes.Object,
            Mock.Of<IProductionOrdersRepository>(),
            reasonCodes.Object,
            MockGuids(),
            MockClock(),
            MockTenant().Object);

        // Act
        await handler.Handle(
            new StartDowntimeEventRequest(
                machineId, reasonId, _now.AddHours(-1), null, null),
            CancellationToken.None);

        // Assert
        capture.Single(MesMeters.DowntimeCounterName, machineId).Value.Should().Be(1);
        capture.Single(MesMeters.DowntimeCounterName, machineId).Tag(MesMeters.WorkCenterLabel)
            .Should().Be(machineId.ToString("D"));
        capture.Single(MesMeters.DowntimeCounterName, machineId).Tag(MesMeters.ReasonCodeLabel)
            .Should().Be("DT-MAINT-BREAKDOWN");
    }

    [Fact]
    public async Task GetOeeSnapshot_EmitsDurationHistogram_WithWorkCenterLabel()
    {
        // Arrange
        using var capture = new MeterCapture();
        var machineId = Guid.NewGuid();
        var machines = new Mock<IMachinesRepository>();
        machines.Setup(r => r.GetByIdAsync(machineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Machine
            {
                Id = machineId,
                TenantId = _tenantId,
                Code = "WC-METER",
                Name = "Meter Work Center"
            });
        var downtimes = new Mock<IDowntimeEventsRepository>();
        downtimes.Setup(r => r.ListOverlappingAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var confirmations = new Mock<IProductionConfirmationsRepository>();
        confirmations.Setup(r => r.ListForMachineInWindowAsync(
                machineId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var handler = new GetOeeSnapshotRequestHandler(
            machines.Object,
            Mock.Of<IWorkCenterCalendarsRepository>(),
            downtimes.Object,
            confirmations.Object,
            MockTenant().Object);

        // Act
        await handler.Handle(
            new GetOeeSnapshotRequest(
                machineId, _now.AddHours(-8), _now, 60m),
            CancellationToken.None);

        // Assert
        var snapshot = capture.Single(MesMeters.OeeSnapshotHistogramName, machineId);
        snapshot.Value.Should().BeGreaterThanOrEqualTo(0);
        snapshot.Tag(MesMeters.WorkCenterLabel).Should().Be(machineId.ToString("D"));
        snapshot.Tag(MesMeters.TenantLabel).Should().Be(_tenantId.ToString("D"));
    }

    private Mock<IProductionOrdersRepository> MockOrders(ProductionOrder order)
    {
        var orders = new Mock<IProductionOrdersRepository>();
        orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        return orders;
    }

    private static IChildEntitiesRepository MockChildEntities()
    {
        var children = new Mock<IChildEntitiesRepository>();
        children.Setup(r => r.ListBomItemsForVersionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BomItem>());
        return children.Object;
    }

    private IGuidProvider MockGuids()
    {
        var guids = new Mock<IGuidProvider>();
        guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        return guids.Object;
    }

    private IDateTimeProvider MockClock()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);
        return clock.Object;
    }

    private Mock<ITenantContext> MockTenant()
    {
        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
        return tenant;
    }

    private sealed class MeterCapture : IDisposable
    {
        public sealed record Captured(string Instrument, double Value, IReadOnlyList<KeyValuePair<string, object?>> Tags)
        {
            public string? Tag(string key) =>
                Tags.FirstOrDefault(t => t.Key == key).Value?.ToString();
        }

        private readonly List<Captured> _measurements = new();
        private readonly MeterListener _listener = new();

        public MeterCapture()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == MesMeters.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>(Record);
            _listener.SetMeasurementEventCallback<double>(Record);
            _listener.Start();
        }

        public Captured Single(string instrument, Guid machineId) =>
            _measurements
                .Where(m => m.Instrument == instrument
                    && m.Tag(MesMeters.WorkCenterLabel) == machineId.ToString("D"))
                .Should().ContainSingle().Subject;

        public void Dispose() => _listener.Dispose();

        private void Record<T>(Instrument instrument, T measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? _)
            where T : struct
        {
            lock (_measurements)
            {
                _measurements.Add(new Captured(instrument.Name, Convert.ToDouble(measurement), tags.ToArray()));
            }
        }
    }
}
