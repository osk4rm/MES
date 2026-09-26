using AsistOff.MES.Shared.Abstractions.Models.DomainEvents;
using AsistOff.MES.Shared.Infrastructure.Outbox;
using AsistOff.MES.Shared.Infrastructure.Persistence.Entities;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Outbox;

/// <summary>
/// Slice 2 (#259) relay-policy coverage: the relay query excludes parked
/// poison rows while the legacy overload keeps its behavior; the relay
/// options carry safe defaults for poll interval, batch size and max attempts.
/// </summary>
public class OutboxRelayPolicyTests
{
    private sealed record WidgetCreatedEvent(Guid WidgetId, string Name) : IDomainEvent;

    private static readonly DateTime Occurred = new(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ApplyUndispatched_WithMaxAttempts_ExcludesParkedPoisonRows()
    {
        // Arrange — retry counts straddling the budget, plus one dispatched row.
        var tenantId = Guid.NewGuid();
        var rows = new[]
        {
            Row(tenantId, Occurred.AddHours(4), retryCount: 5),
            Row(tenantId, Occurred.AddHours(1), retryCount: 0),
            Row(tenantId, Occurred.AddHours(2), retryCount: 2),
            Row(tenantId, Occurred.AddHours(3), retryCount: 3),
            Row(tenantId, Occurred.AddHours(0), dispatched: true),
        }.AsQueryable();

        // Act
        var page = OutboxStager.ApplyUndispatched(rows, 100, 3).ToList();

        // Assert — parked (3, 5) and dispatched rows excluded, oldest first.
        page.Select(x => x.RetryCount).Should().Equal(0, 2);
        page.Select(x => x.OccurredOnUtc).Should().BeInAscendingOrder();
    }

    [Fact]
    public void ApplyUndispatched_LegacyOverload_KeepsPreviousBehavior()
    {
        // Arrange — the two-argument helper predates poison parking.
        var tenantId = Guid.NewGuid();
        var rows = new[]
        {
            Row(tenantId, Occurred.AddHours(1), retryCount: 7),
            Row(tenantId, Occurred.AddHours(2), dispatched: true),
        }.AsQueryable();

        // Act
        var page = OutboxStager.ApplyUndispatched(rows, 100).ToList();

        // Assert — every undispatched row is returned regardless of retry count.
        page.Should().ContainSingle(x => x.RetryCount == 7);
    }

    [Fact]
    public void OutboxRelayOptions_HaveSafeDefaults()
    {
        // Act
        var options = new OutboxRelayOptions();

        // Assert
        options.Enabled.Should().BeTrue();
        options.PollIntervalSeconds.Should().Be(10);
        options.BatchSize.Should().Be(100);
        options.MaxAttempts.Should().Be(5);
        OutboxRelayOptions.SectionName.Should().Be("Outbox");
    }

    [Fact]
    public void ApplyUndispatched_ZeroBatchSize_ClampsToSingleRow()
    {
        // Arrange — five eligible rows, batch size below the 1..500 range.
        var tenantId = Guid.NewGuid();
        var rows = Enumerable.Range(0, 5)
            .Select(i => Row(tenantId, Occurred.AddMinutes(i)))
            .AsQueryable();

        // Act
        var page = OutboxStager.ApplyUndispatched(rows, 0, 5).ToList();

        // Assert — the relay still makes progress one row at a time.
        page.Should().ContainSingle();
    }

    [Fact]
    public void ApplyUndispatched_OversizedBatchSize_CapsAtMaxBatchSize()
    {
        // Arrange — more eligible rows than a single cycle may take.
        var tenantId = Guid.NewGuid();
        var rows = Enumerable.Range(0, OutboxStager.MaxBatchSize + 100)
            .Select(i => Row(tenantId, Occurred.AddMinutes(i)))
            .AsQueryable();

        // Act
        var page = OutboxStager.ApplyUndispatched(rows, 10_000, 5).ToList();

        // Assert — bounded batches keep every cycle finite.
        page.Should().HaveCount(OutboxStager.MaxBatchSize);
    }

    private static OutboxMessage Row(Guid tenantId, DateTime occurred, int retryCount = 0, bool dispatched = false) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        IdempotencyKey = Guid.NewGuid().ToString("N"),
        Type = typeof(WidgetCreatedEvent).AssemblyQualifiedName!,
        Payload = "{}",
        OccurredOnUtc = occurred,
        Dispatched = dispatched,
        RetryCount = retryCount,
    };
}
