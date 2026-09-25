using System.Text.Json;
using AsistOff.MES.Shared.Abstractions.Models.DomainEvents;
using AsistOff.MES.Shared.Infrastructure.Outbox;
using AsistOff.MES.Shared.Infrastructure.Persistence.Entities;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsistOff.MES.Shared.Tests.Outbox;

/// <summary>
/// Slice 2 (#259) relay-dispatcher coverage: success marks the row dispatched;
/// transient handler failures increment the retry count and wait with
/// exponential backoff before succeeding; persistent failures park the row as
/// poison after the attempt budget; oversized payloads, unknown event types
/// and corrupt payloads park immediately with a clear error that carries the
/// event type and outbox id but never the payload.
/// </summary>
public class OutboxDispatcherTests
{
    private sealed record WidgetCreatedEvent(Guid WidgetId, string Name) : IDomainEvent;

    private sealed record HugeNoteEvent(string Notes) : IDomainEvent;

    private sealed class CapturedLog
    {
        public LogLevel Level { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    private sealed class ListLogger<T>(List<CapturedLog> sink) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => sink.Add(new CapturedLog { Level = logLevel, Message = formatter(state, exception) });
    }

    private sealed class NoDelayDispatcher(ILogger<OutboxDispatcher> logger) : OutboxDispatcher(logger)
    {
        public List<TimeSpan> Delays { get; } = new();

        protected override Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            return Task.CompletedTask;
        }
    }

    private readonly List<CapturedLog> _logs = new();
    private readonly Mock<IPublisher> _publisher = new();

    private NoDelayDispatcher BuildDispatcher() => new(new ListLogger<OutboxDispatcher>(_logs));

    private static OutboxMessage Row(object @event, Type eventType, int retryCount = 0) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        IdempotencyKey = Guid.NewGuid().ToString("N"),
        Type = eventType.AssemblyQualifiedName!,
        Payload = JsonSerializer.Serialize(@event, eventType),
        OccurredOnUtc = new DateTime(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc),
        Dispatched = false,
        RetryCount = retryCount,
    };

    [Fact]
    public async Task DispatchRow_Success_MarksDispatched()
    {
        // Arrange
        var dispatcher = BuildDispatcher();
        var @event = new WidgetCreatedEvent(Guid.NewGuid(), "gadget");
        var row = Row(@event, typeof(WidgetCreatedEvent));
        var saves = 0;

        // Act
        var dispatched = await dispatcher.DispatchRowAsync(
            row, _publisher.Object, 5, _ => { saves++; return Task.CompletedTask; });

        // Assert
        dispatched.Should().BeTrue();
        row.Dispatched.Should().BeTrue();
        row.RetryCount.Should().Be(0);
        saves.Should().Be(1);
        _publisher.Verify(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        _logs.Should().ContainSingle(x => x.Level == LogLevel.Debug && x.Message.Contains(row.Id.ToString()));
    }

    [Fact]
    public async Task DispatchRow_TransientFailuresThenSuccess_IncrementsRetry_AndWaitsWithBackoff()
    {
        // Arrange — the handler fails twice, then recovers.
        var dispatcher = BuildDispatcher();
        var @event = new WidgetCreatedEvent(Guid.NewGuid(), "gadget");
        var row = Row(@event, typeof(WidgetCreatedEvent));
        _publisher.SetupSequence(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker down"))
            .ThrowsAsync(new InvalidOperationException("broker down"))
            .Returns(Task.CompletedTask);
        var saves = 0;

        // Act
        var dispatched = await dispatcher.DispatchRowAsync(
            row, _publisher.Object, 5, _ => { saves++; return Task.CompletedTask; });

        // Assert — retried with exponential backoff, then delivered exactly once.
        dispatched.Should().BeTrue();
        row.Dispatched.Should().BeTrue();
        row.RetryCount.Should().Be(2);
        saves.Should().Be(3);
        dispatcher.Delays.Should().Equal(
            OutboxDispatcher.ComputeRetryDelay(1),
            OutboxDispatcher.ComputeRetryDelay(2));
        _publisher.Verify(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task DispatchRow_PersistentFailure_ParksPoisonAfterMaxAttempts()
    {
        // Arrange — the handler never recovers and the payload carries a marker
        // that must never leak into the logs.
        var dispatcher = BuildDispatcher();
        var @event = new WidgetCreatedEvent(Guid.NewGuid(), "SUPER-SECRET-PAYLOAD-123");
        var row = Row(@event, typeof(WidgetCreatedEvent));
        _publisher.Setup(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker down"));
        var saves = 0;

        // Act
        var dispatched = await dispatcher.DispatchRowAsync(
            row, _publisher.Object, 3, _ => { saves++; return Task.CompletedTask; });

        // Assert — parked after exactly the budgeted attempts.
        dispatched.Should().BeFalse();
        row.Dispatched.Should().BeFalse();
        row.RetryCount.Should().Be(3);
        saves.Should().Be(3);
        _publisher.Verify(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Exactly(3));

        var poison = _logs.Should().ContainSingle(
            x => x.Level == LogLevel.Error && x.Message.Contains("poison")).Subject;
        poison.Message.Should().Contain(nameof(WidgetCreatedEvent));
        poison.Message.Should().Contain(row.Id.ToString());
        _logs.Select(x => x.Message).Should().NotContain(x => x.Contains("SUPER-SECRET-PAYLOAD-123"));
    }

    [Fact]
    public async Task DispatchRow_UnknownEventType_ParksPoisonImmediately()
    {
        // Arrange
        var dispatcher = BuildDispatcher();
        var row = Row(new WidgetCreatedEvent(Guid.NewGuid(), "gadget"), typeof(WidgetCreatedEvent));
        row.Type = "Missing.Event, MissingAssembly";
        row.Payload = """{"Marker":"SUPER-SECRET-PAYLOAD-123"}""";
        var saves = 0;

        // Act
        var dispatched = await dispatcher.DispatchRowAsync(
            row, _publisher.Object, 5, _ => { saves++; return Task.CompletedTask; });

        // Assert — parked without a single delivery attempt.
        dispatched.Should().BeFalse();
        row.Dispatched.Should().BeFalse();
        row.RetryCount.Should().Be(5);
        saves.Should().Be(1);
        _publisher.Verify(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);

        var poison = _logs.Should().ContainSingle(
            x => x.Level == LogLevel.Error && x.Message.Contains("poison")).Subject;
        poison.Message.Should().Contain("Missing.Event");
        poison.Message.Should().Contain(row.Id.ToString());
        _logs.Select(x => x.Message).Should().NotContain(x => x.Contains("SUPER-SECRET-PAYLOAD-123"));
    }

    [Fact]
    public async Task DispatchRow_CorruptPayload_ParksPoisonWithoutPublishing()
    {
        // Arrange
        var dispatcher = BuildDispatcher();
        var row = Row(new WidgetCreatedEvent(Guid.NewGuid(), "gadget"), typeof(WidgetCreatedEvent));
        row.Payload = "{not-json";
        var saves = 0;

        // Act
        var dispatched = await dispatcher.DispatchRowAsync(
            row, _publisher.Object, 5, _ => { saves++; return Task.CompletedTask; });

        // Assert
        dispatched.Should().BeFalse();
        row.Dispatched.Should().BeFalse();
        row.RetryCount.Should().Be(5);
        saves.Should().Be(1);
        _publisher.Verify(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        _logs.Should().ContainSingle(
            x => x.Level == LogLevel.Error && x.Message.Contains("poison") && x.Message.Contains(row.Id.ToString()));
    }

    [Fact]
    public async Task DispatchRow_OversizedPayload_ParksPoisonWithoutPublishing()
    {
        // Arrange — a row that could never have passed staging (e.g. written
        // before the cap) must not be delivered.
        var dispatcher = BuildDispatcher();
        var @event = new HugeNoteEvent(new string('n', OutboxStager.MaxPayloadLength + 1));
        var row = Row(@event, typeof(HugeNoteEvent));
        var saves = 0;

        // Act
        var dispatched = await dispatcher.DispatchRowAsync(
            row, _publisher.Object, 5, _ => { saves++; return Task.CompletedTask; });

        // Assert
        dispatched.Should().BeFalse();
        row.Dispatched.Should().BeFalse();
        row.RetryCount.Should().Be(5);
        saves.Should().Be(1);
        _publisher.Verify(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        _logs.Should().ContainSingle(
            x => x.Level == LogLevel.Error && x.Message.Contains("poison") && x.Message.Contains(row.Id.ToString()));
    }

    [Fact]
    public async Task DispatchRow_Cancelled_PropagatesWithoutMutating()
    {
        // Arrange
        var dispatcher = BuildDispatcher();
        var @event = new WidgetCreatedEvent(Guid.NewGuid(), "gadget");
        var row = Row(@event, typeof(WidgetCreatedEvent));
        var saves = 0;
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = () => dispatcher.DispatchRowAsync(
            row, _publisher.Object, 5, _ => { saves++; return Task.CompletedTask; }, cts.Token);

        // Assert — graceful shutdown never half-delivers.
        await act.Should().ThrowAsync<OperationCanceledException>();
        row.Dispatched.Should().BeFalse();
        row.RetryCount.Should().Be(0);
        saves.Should().Be(0);
        _publisher.Verify(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(4, 16)]
    public void ComputeRetryDelay_IsExponential(int retryCount, double expectedSeconds)
    {
        // Act
        var delay = OutboxDispatcher.ComputeRetryDelay(retryCount);

        // Assert
        delay.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public void ComputeRetryDelay_IsCapped()
    {
        // Act
        var delay = OutboxDispatcher.ComputeRetryDelay(100);

        // Assert
        delay.Should().Be(OutboxDispatcher.RetryMaxDelay);
    }
}
