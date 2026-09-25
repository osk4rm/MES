using AsistOff.MES.Multitenancy.Repositories;
using AsistOff.MES.Production.Application.Telemetry;
using AsistOff.MES.Production.Domain.Entities;
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

public class OpcUaPollingServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
    private readonly ILogger<OpcUaPollingService> _logger = NullLogger<OpcUaPollingService>.Instance;

    private static Mock<IOptionsMonitor<OpcUaPollingOptions>> Options(bool enabled, int intervalSeconds = 30)
    {
        var options = new Mock<IOptionsMonitor<OpcUaPollingOptions>>();
        options.SetupGet(o => o.CurrentValue).Returns(new OpcUaPollingOptions
        {
            PollingEnabled = enabled,
            PollingIntervalSeconds = intervalSeconds
        });
        return options;
    }

    private static OpcUaConnection Connection(Guid id, bool isEnabled = true) => new()
    {
        Id = id,
        TenantId = Guid.NewGuid(),
        MachineId = Guid.NewGuid(),
        EndpointUrl = "opc.tcp://plc-1:4840",
        IsEnabled = isEnabled
    };

    private OpcUaPollingService CreateSut(
        Mock<IServiceScopeFactory> scopeFactory, Mock<IOptionsMonitor<OpcUaPollingOptions>> options)
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);
        return new OpcUaPollingService(scopeFactory.Object, options.Object, clock.Object, _logger);
    }

    [Fact]
    public async Task PollOnceAsync_PollingDisabled_WritesNothing()
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
    public async Task PollOnceAsync_Enabled_PollsEachActiveTenantWithOwnTenantId()
    {
        // Arrange
        var otherTenantId = Guid.NewGuid();
        var connection = Connection(Guid.NewGuid());

        var tenants = new Mock<ITenantRepository>();
        tenants.Setup(t => t.ListActiveIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _tenantId, otherTenantId });

        var connections = new Mock<IOpcUaConnectionsRepository>();
        connections.Setup(r => r.ListEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { connection });

        var reader = new Mock<IOpcUaReader>();
        reader.Setup(r => r.PollAsync(It.IsAny<OpcUaConnection>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(ITenantRepository))).Returns(tenants.Object);
        provider.Setup(p => p.GetService(typeof(IOpcUaConnectionsRepository))).Returns(connections.Object);
        provider.Setup(p => p.GetService(typeof(IOpcUaReader))).Returns(reader.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(provider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        var sut = CreateSut(scopeFactory, Options(enabled: true));

        // Act
        var written = await sut.PollOnceAsync();

        // Assert
        written.Should().Be(4);
        reader.Verify(r => r.PollAsync(connection, _tenantId, It.IsAny<CancellationToken>()), Times.Once);
        reader.Verify(r => r.PollAsync(connection, otherTenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PollTenantAsync_DisabledConnections_SkippedWithoutWrite()
    {
        // Arrange
        var enabled = Connection(Guid.NewGuid(), isEnabled: true);
        var disabled = Connection(Guid.NewGuid(), isEnabled: false);

        var connections = new Mock<IOpcUaConnectionsRepository>();
        connections.Setup(r => r.ListEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { enabled, disabled });

        var reader = new Mock<IOpcUaReader>();
        reader.Setup(r => r.PollAsync(It.IsAny<OpcUaConnection>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var written = await OpcUaPollingService.PollTenantAsync(
            connections.Object, reader.Object, _tenantId, _now, _logger);

        // Assert
        written.Should().Be(1);
        reader.Verify(r => r.PollAsync(
            enabled, _tenantId, It.IsAny<CancellationToken>()), Times.Once);
        reader.Verify(r => r.PollAsync(
            disabled, It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        connections.Verify(r => r.UpdateAsync(disabled, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PollTenantAsync_Success_UpdatesLastSeenAtAndClearsLastError()
    {
        // Arrange
        var connection = Connection(Guid.NewGuid());
        connection.LastError = "previous failure";

        var connections = new Mock<IOpcUaConnectionsRepository>();
        connections.Setup(r => r.ListEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { connection });

        var reader = new Mock<IOpcUaReader>();
        reader.Setup(r => r.PollAsync(connection, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        // Act
        var written = await OpcUaPollingService.PollTenantAsync(
            connections.Object, reader.Object, _tenantId, _now, _logger);

        // Assert
        written.Should().Be(3);
        connection.LastSeenAtUtc.Should().Be(_now);
        connection.LastError.Should().BeNull();
        connections.Verify(r => r.UpdateAsync(connection, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PollTenantAsync_Failure_SetsLastErrorAndContinuesBatch()
    {
        // Arrange
        var failing = Connection(Guid.NewGuid());
        var healthy = Connection(Guid.NewGuid());

        var connections = new Mock<IOpcUaConnectionsRepository>();
        connections.Setup(r => r.ListEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { failing, healthy });

        var reader = new Mock<IOpcUaReader>();
        reader.Setup(r => r.PollAsync(failing, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        reader.Setup(r => r.PollAsync(healthy, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        // Act
        var written = await OpcUaPollingService.PollTenantAsync(
            connections.Object, reader.Object, _tenantId, _now, _logger);

        // Assert
        written.Should().Be(2);
        failing.LastError.Should().Be("boom");
        failing.LastSeenAtUtc.Should().BeNull();
        healthy.LastSeenAtUtc.Should().Be(_now);
        healthy.LastError.Should().BeNull();
    }
}
