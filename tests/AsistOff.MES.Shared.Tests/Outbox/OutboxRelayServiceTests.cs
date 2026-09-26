using AsistOff.MES.Shared.Infrastructure.Outbox;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AsistOff.MES.Shared.Tests.Outbox;

/// <summary>
/// Slice 2 (#259) relay-lifecycle coverage: the timer loop honors the
/// configured poll interval (clamped to 1..3600s), exits early when disabled,
/// isolates per-tenant failures, propagates cancellation, and stops
/// gracefully on shutdown — all without a database or a real timer.
/// </summary>
public class OutboxRelayServiceTests
{
    private sealed class StubRelayService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<OutboxRelayOptions> options,
        ILogger<OutboxRelayService> logger,
        Func<Guid, CancellationToken, Task<int>> relayTenant)
        : OutboxRelayService(scopeFactory, options, logger)
    {
        public List<Guid> RelayedTenants { get; } = new();

        public override Task<int> RelayTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            RelayedTenants.Add(tenantId);
            return relayTenant(tenantId, cancellationToken);
        }
    }

    [Fact]
    public void ResolvePollDelay_Default_IsTenSeconds()
    {
        // Act
        var delay = OutboxRelayService.ResolvePollDelay(new OutboxRelayOptions());

        // Assert
        delay.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(30, 30)]
    [InlineData(3600, 3600)]
    [InlineData(10000, 3600)]
    public void ResolvePollDelay_ClampsTo1Through3600Seconds(int configured, int expectedSeconds)
    {
        // Act
        var delay = OutboxRelayService.ResolvePollDelay(new OutboxRelayOptions { PollIntervalSeconds = configured });

        // Assert — the live loop never polls faster than 1s or slower than 1h.
        delay.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public async Task RelayOnceAsync_NoTenants_ReturnsZeroWithoutRelaying()
    {
        // Arrange
        var (factory, source) = BuildScopeFactory([]);
        var service = new StubRelayService(
            factory.Object, MonitorOf(new OutboxRelayOptions()), Mock.Of<ILogger<OutboxRelayService>>(),
            (_, _) => Task.FromResult(1));

        // Act
        var total = await service.RelayOnceAsync();

        // Assert
        total.Should().Be(0);
        service.RelayedTenants.Should().BeEmpty();
        source.Verify(s => s.ListActiveTenantIdsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RelayOnceAsync_FailingTenant_DoesNotAbortOthers()
    {
        // Arrange — tenant A always throws, tenant B dispatches one row.
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var (factory, _) = BuildScopeFactory([tenantA, tenantB]);
        var service = new StubRelayService(
            factory.Object, MonitorOf(new OutboxRelayOptions()), Mock.Of<ILogger<OutboxRelayService>>(),
            (tenantId, _) => tenantId == tenantA
                ? Task.FromException<int>(new InvalidOperationException("tenant A store down"))
                : Task.FromResult(1));

        // Act
        var total = await service.RelayOnceAsync();

        // Assert — B still relayed despite A's failure.
        total.Should().Be(1);
        service.RelayedTenants.Should().Equal(tenantA, tenantB);
    }

    [Fact]
    public async Task RelayOnceAsync_CancelledTenant_PropagatesCancellation()
    {
        // Arrange — tenant A honors shutdown, tenant B must never be touched.
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var (factory, _) = BuildScopeFactory([tenantA, tenantB]);
        var service = new StubRelayService(
            factory.Object, MonitorOf(new OutboxRelayOptions()), Mock.Of<ILogger<OutboxRelayService>>(),
            (tenantId, _) => tenantId == tenantA
                ? Task.FromException<int>(new OperationCanceledException())
                : Task.FromResult(1));

        // Act
        var act = () => service.RelayOnceAsync();

        // Assert — cancellation is never swallowed as a per-tenant failure.
        await act.Should().ThrowAsync<OperationCanceledException>();
        service.RelayedTenants.Should().Equal(tenantA);
    }

    [Fact]
    public async Task RelayOnceAsync_PreCancelledToken_ThrowsBeforeRelaying()
    {
        // Arrange
        var tenant = Guid.NewGuid();
        var (factory, _) = BuildScopeFactory([tenant]);
        var service = new StubRelayService(
            factory.Object, MonitorOf(new OutboxRelayOptions()), Mock.Of<ILogger<OutboxRelayService>>(),
            (_, _) => Task.FromResult(1));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = () => service.RelayOnceAsync(cts.Token);

        // Assert — graceful shutdown never starts new tenant work.
        await act.Should().ThrowAsync<OperationCanceledException>();
        service.RelayedTenants.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_DisabledRelay_ExitsWithoutListingTenants()
    {
        // Arrange
        var (factory, source) = BuildScopeFactory([Guid.NewGuid()]);
        var service = new StubRelayService(
            factory.Object,
            MonitorOf(new OutboxRelayOptions { Enabled = false }),
            Mock.Of<ILogger<OutboxRelayService>>(),
            (_, _) => Task.FromResult(1));

        // Act — start the background loop, let the early exit run, stop.
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        await service.StopAsync(CancellationToken.None);

        // Assert — the tenant source was never consulted.
        source.Verify(s => s.ListActiveTenantIdsAsync(It.IsAny<CancellationToken>()), Times.Never);
        service.RelayedTenants.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_EnabledRelay_StopsGracefullyBeforeFirstCycle()
    {
        // Arrange — an hour-long interval, so stopping must cancel the
        // delay-first wait instead of running a cycle.
        var (factory, source) = BuildScopeFactory([Guid.NewGuid()]);
        var service = new StubRelayService(
            factory.Object,
            MonitorOf(new OutboxRelayOptions { Enabled = true, PollIntervalSeconds = 3600 }),
            Mock.Of<ILogger<OutboxRelayService>>(),
            (_, _) => Task.FromResult(1));

        // Act — start and immediately stop; both must complete promptly.
        await service.StartAsync(CancellationToken.None);
        var stop = service.StopAsync(CancellationToken.None);
        var completed = await Task.WhenAny(stop, Task.Delay(TimeSpan.FromSeconds(10)));

        // Assert — shutdown won the race and no cycle ever ran.
        completed.Should().BeSameAs(stop);
        source.Verify(s => s.ListActiveTenantIdsAsync(It.IsAny<CancellationToken>()), Times.Never);
        service.RelayedTenants.Should().BeEmpty();
    }

    private static (Mock<IServiceScopeFactory> Factory, Mock<IOutboxTenantSource> Source) BuildScopeFactory(
        IReadOnlyCollection<Guid> tenantIds)
    {
        var source = new Mock<IOutboxTenantSource>();
        source.Setup(s => s.ListActiveTenantIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tenantIds);

        var services = new ServiceCollection();
        services.AddSingleton(source.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(services.BuildServiceProvider());

        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(f => f.CreateScope()).Returns(scope.Object);

        return (factory, source);
    }

    private static IOptionsMonitor<OutboxRelayOptions> MonitorOf(OutboxRelayOptions options)
    {
        var monitor = new Mock<IOptionsMonitor<OutboxRelayOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(options);

        return monitor.Object;
    }
}
