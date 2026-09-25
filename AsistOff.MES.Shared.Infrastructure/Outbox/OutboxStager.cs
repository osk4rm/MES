using System.Reflection;
using System.Text.Json;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Models.DomainEvents;
using AsistOff.MES.Shared.Infrastructure.Persistence.Entities;

namespace AsistOff.MES.Shared.Infrastructure.Outbox;

/// <summary>
/// Slice-1 (#258) staging helper mapping domain events to durable
/// <see cref="OutboxMessage"/> rows. Pure (no database): one event maps to
/// exactly one row, an empty batch stages nothing.
///
/// Safety guards (both surface as <see cref="ValidationException"/> with a
/// clear message, so the offending primary write is rejected instead of
/// persisting a leaking or unreadable row):
/// <list type="bullet">
/// <item>Secret scan: any public event member whose name contains a
/// password/secret/token-like fragment rejects the event. Domain events are
/// cross-module facts and must never carry credentials.</item>
/// <item>Size cap: serialized payloads longer than
/// <see cref="MaxPayloadLength"/> characters are rejected.</item>
/// </list>
///
/// <see cref="ApplyUndispatched"/> is the slice-2 relay query: undispatched
/// rows oldest-first with a bounded batch size. Tenant isolation rides on the
/// <c>DefaultContext</c> global query filter, so no manual
/// <c>TenantId</c> predicate is added here.
/// </summary>
public static class OutboxStager
{
    /// <summary>Maximum accepted serialized event payload, in characters.</summary>
    public const int MaxPayloadLength = 64_000;

    /// <summary>Upper bound for a single relay batch.</summary>
    public const int MaxBatchSize = 500;

    private static readonly string[] SecretFragments =
    [
        "password", "passwd", "pwd", "secret", "token", "apikey", "api_key",
        "connectionstring", "privatekey", "clientsecret", "credential",
    ];

    /// <summary>
    /// Maps each event to one undispatched <see cref="OutboxMessage"/> sharing
    /// <paramref name="tenantId"/> and <paramref name="occurredOnUtc"/>.
    /// </summary>
    /// <exception cref="ValidationException">
    /// Thrown when an event carries secret-like members, cannot be serialized,
    /// or exceeds <see cref="MaxPayloadLength"/>.
    /// </exception>
    public static IReadOnlyList<OutboxMessage> Stage(
        IEnumerable<IDomainEvent> events,
        Guid tenantId,
        DateTime occurredOnUtc,
        Func<Guid> newGuid)
    {
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Cannot stage outbox messages without a tenant context.");
        }

        var staged = new List<OutboxMessage>();

        foreach (var @event in events)
        {
            var eventType = @event.GetType();
            RejectSecrets(eventType);
            var payload = Serialize(@event, eventType);
            RejectOversized(eventType, payload);

            staged.Add(new OutboxMessage
            {
                Id = newGuid(),
                TenantId = tenantId,
                IdempotencyKey = newGuid().ToString("N"),
                Type = eventType.AssemblyQualifiedName ?? eventType.FullName ?? eventType.Name,
                Payload = payload,
                OccurredOnUtc = occurredOnUtc,
                Dispatched = false,
                RetryCount = 0,
            });
        }

        return staged;
    }

    /// <summary>
    /// Restricts the query to undispatched rows ordered by occurrence time
    /// (oldest first, <c>Id</c> as tiebreak) with the batch size clamped to
    /// <c>1..MaxBatchSize</c>.
    /// </summary>
    public static IQueryable<OutboxMessage> ApplyUndispatched(
        IQueryable<OutboxMessage> query,
        int batchSize)
    {
        var take = Math.Clamp(batchSize, 1, MaxBatchSize);

        return query
            .Where(x => !x.Dispatched)
            .OrderBy(x => x.OccurredOnUtc)
            .ThenBy(x => x.Id)
            .Take(take);
    }

    private static void RejectSecrets(Type eventType)
    {
        var offending = eventType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(p => p.Name)
            .FirstOrDefault(name => SecretFragments.Any(
                fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase)));

        if (offending is not null)
        {
            throw new ValidationException(
                nameof(OutboxMessage.Payload),
                $"Domain event '{eventType.FullName}' carries secret-like member '{offending}' and cannot be staged to the outbox.");
        }
    }

    private static string Serialize(IDomainEvent @event, Type eventType)
    {
        try
        {
            return JsonSerializer.Serialize(@event, eventType);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException)
        {
            throw new ValidationException(
                nameof(OutboxMessage.Payload),
                $"Domain event '{eventType.FullName}' cannot be serialized to the outbox: {ex.Message}");
        }
    }

    private static void RejectOversized(Type eventType, string payload)
    {
        if (payload.Length > MaxPayloadLength)
        {
            throw new ValidationException(
                nameof(OutboxMessage.Payload),
                $"Domain event '{eventType.FullName}' payload of {payload.Length} characters exceeds the maximum of {MaxPayloadLength} characters and cannot be staged to the outbox.");
        }
    }
}
