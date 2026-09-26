using System.Text.Json;
using AsistOff.MES.Shared.Abstractions.Models.DomainEvents;
using AsistOff.MES.Shared.Infrastructure.Persistence.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Shared.Infrastructure.Outbox;

/// <summary>
/// Slice 2 (#259) per-row outbox dispatcher. Resolves the stored event type,
/// deserializes the stored payload, and publishes it through MediatR
/// <i>after</i> the staging transaction committed, so a rolled-back write can
/// never produce a dispatch. The caller owns the <c>DbContext</c> (and its
/// ambient tenant scope); this class only mutates the tracked
/// <see cref="OutboxMessage"/> and invokes the provided save callback.
///
/// Outcome contract (no schema change in this slice, so no new columns):
/// <list type="bullet">
/// <item>Success: <c>Dispatched = true</c>. The mark itself is the observable
/// delivery record; there is no dispatch-timestamp column.</item>
/// <item>Transient handler failure: <c>RetryCount</c> is incremented and
/// persisted, then the publish is retried after an exponential backoff
/// (<see cref="ComputeRetryDelay"/>).</item>
/// <item>Poison: once <c>RetryCount</c> reaches the attempt budget — or
/// immediately for oversized payloads, unresolvable event types, and
/// corrupt payloads, which can never succeed on retry — the row is parked
/// with <c>RetryCount</c> pinned to the budget so the relay query
/// (<see cref="OutboxStager.ApplyUndispatched(IQueryable{OutboxMessage},int,int)"/>)
/// never refetches it, and an error is logged with the event type and
/// outbox id but never the payload.</item>
/// </list>
/// </summary>
public class OutboxDispatcher(ILogger<OutboxDispatcher> logger)
{
    /// <summary>Base backoff between delivery retries.</summary>
    public static readonly TimeSpan RetryBaseDelay = TimeSpan.FromSeconds(2);

    /// <summary>Upper bound for the backoff between delivery retries.</summary>
    public static readonly TimeSpan RetryMaxDelay = TimeSpan.FromMinutes(5);

    /// <summary>Upper bound for the configured per-row attempt budget.</summary>
    public const int MaxAttemptsLimit = 100;

    /// <summary>
    /// Exponential backoff before the next delivery attempt:
    /// 2s, 4s, 8s, … capped at <see cref="RetryMaxDelay"/>.
    /// </summary>
    public static TimeSpan ComputeRetryDelay(int retryCount)
    {
        var seconds = RetryBaseDelay.TotalSeconds * Math.Pow(2, Math.Max(0, retryCount - 1));
        return TimeSpan.FromSeconds(Math.Min(seconds, RetryMaxDelay.TotalSeconds));
    }

    /// <summary>
    /// Dispatches one tracked outbox row. Returns <c>true</c> when the row was
    /// marked dispatched. Mutations are persisted through
    /// <paramref name="saveChangesAsync"/> after every state transition, so a
    /// crash never loses retry progress.
    /// </summary>
    public async Task<bool> DispatchRowAsync(
        OutboxMessage message,
        IPublisher publisher,
        int maxAttempts,
        Func<CancellationToken, Task> saveChangesAsync,
        CancellationToken cancellationToken = default)
    {
        var attempts = Math.Clamp(maxAttempts, 1, MaxAttemptsLimit);

        if (message.Payload.Length > OutboxStager.MaxPayloadLength)
        {
            await ParkPoisonAsync(
                message,
                attempts,
                $"payload of {message.Payload.Length} characters exceeds the maximum of {OutboxStager.MaxPayloadLength} characters",
                saveChangesAsync,
                cancellationToken);

            return false;
        }

        var eventType = Type.GetType(message.Type, throwOnError: false);
        if (eventType is null || !typeof(IDomainEvent).IsAssignableFrom(eventType))
        {
            await ParkPoisonAsync(
                message,
                attempts,
                $"unknown event type '{message.Type}'",
                saveChangesAsync,
                cancellationToken);

            return false;
        }

        IDomainEvent @event;
        try
        {
            var deserialized = JsonSerializer.Deserialize(message.Payload, eventType);
            if (deserialized is not IDomainEvent domainEvent)
            {
                throw new JsonException($"Deserialized payload is not an '{eventType.FullName}'.");
            }

            @event = domainEvent;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException)
        {
            await ParkPoisonAsync(
                message,
                attempts,
                $"payload cannot be deserialized as '{eventType.FullName}': {ex.Message}",
                saveChangesAsync,
                cancellationToken);

            return false;
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await publisher.Publish((object)@event, cancellationToken);

                message.Dispatched = true;
                await saveChangesAsync(cancellationToken);
                logger.LogDebug("Dispatched outbox {OutboxId} of type {EventType}", message.Id, message.Type);

                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.RetryCount++;

                if (message.RetryCount >= attempts)
                {
                    await saveChangesAsync(cancellationToken);
                    logger.LogError(
                        ex,
                        "Outbox {OutboxId} of type {EventType} parked as poison after {Attempts} attempts",
                        message.Id,
                        message.Type,
                        message.RetryCount);

                    return false;
                }

                await saveChangesAsync(cancellationToken);
                logger.LogWarning(
                    ex,
                    "Outbox {OutboxId} of type {EventType} dispatch failed (attempt {RetryCount}); retrying",
                    message.Id,
                    message.Type,
                    message.RetryCount);

                await DelayAsync(ComputeRetryDelay(message.RetryCount), cancellationToken);
            }
        }
    }

    /// <summary>
    /// Backoff wait between retries. Virtual so tests can observe (or skip)
    /// the wait without real timers.
    /// </summary>
    protected virtual Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        => Task.Delay(delay, cancellationToken);

    private async Task ParkPoisonAsync(
        OutboxMessage message,
        int maxAttempts,
        string reason,
        Func<CancellationToken, Task> saveChangesAsync,
        CancellationToken cancellationToken)
    {
        // Pin the retry count to the budget so the relay query treats the row
        // as parked and never refetches it. Raising MaxAttempts later
        // re-enlists parked rows automatically.
        message.RetryCount = Math.Max(message.RetryCount, maxAttempts);
        await saveChangesAsync(cancellationToken);

        // The reason carries the event type and outbox id only — never the
        // payload, which may contain business data.
        logger.LogError(
            "Outbox {OutboxId} of type {EventType} parked as poison: {Reason}",
            message.Id,
            message.Type,
            reason);
    }
}
