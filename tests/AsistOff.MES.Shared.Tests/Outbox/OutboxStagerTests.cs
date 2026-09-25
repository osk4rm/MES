using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Models.DomainEvents;
using AsistOff.MES.Shared.Infrastructure.Outbox;
using AsistOff.MES.Shared.Infrastructure.Persistence.Entities;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Outbox;

/// <summary>
/// Slice 1 (#258) staging-helper coverage: one event maps to one undispatched
/// row carrying type, payload, tenant and occurrence time; multiple events map
/// to multiple rows with distinct idempotency keys; empty batches stage
/// nothing; secret-like members and oversized payloads are rejected with a
/// clear error; the undispatched query helper returns oldest-first rows with
/// a bounded batch size.
/// </summary>
public class OutboxStagerTests
{
    private sealed record WidgetCreatedEvent(Guid WidgetId, string Name) : IDomainEvent;

    private sealed record WidgetRenamedEvent(Guid WidgetId, string Name) : IDomainEvent;

    private sealed record PasswordChangedEvent(Guid UserId, string Password) : IDomainEvent;

    private sealed record ConnectionOpenedEvent(Guid MachineId, string ConnectionString) : IDomainEvent;

    private sealed record HugeNoteEvent(string Notes) : IDomainEvent;

    private static readonly DateTime Occurred = new(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Stage_SingleEvent_MapsToSingleUndispatchedRow()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var @event = new WidgetCreatedEvent(Guid.NewGuid(), "gadget");

        // Act
        var staged = OutboxStager.Stage([@event], tenantId, Occurred, Guid.NewGuid);

        // Assert
        staged.Should().ContainSingle();
        var row = staged.Single();
        row.TenantId.Should().Be(tenantId);
        row.Type.Should().Contain(nameof(WidgetCreatedEvent));
        row.Payload.Should().Contain("gadget");
        row.OccurredOnUtc.Should().Be(Occurred);
        row.Dispatched.Should().BeFalse();
        row.RetryCount.Should().Be(0);
        row.Id.Should().NotBe(Guid.Empty);
        row.IdempotencyKey.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Stage_MultipleEvents_MapsToOneRowPerEventWithDistinctKeys()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var first = new WidgetCreatedEvent(Guid.NewGuid(), "first");
        var second = new WidgetRenamedEvent(first.WidgetId, "second");

        // Act
        var staged = OutboxStager.Stage([first, second], tenantId, Occurred, Guid.NewGuid);

        // Assert
        staged.Should().HaveCount(2);
        staged.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        staged.Select(x => x.IdempotencyKey).Should().OnlyHaveUniqueItems();
        staged.Select(x => x.Type).Should().Contain(t => t.Contains(nameof(WidgetCreatedEvent)));
        staged.Select(x => x.Type).Should().Contain(t => t.Contains(nameof(WidgetRenamedEvent)));
        staged.Should().OnlyContain(x => x.TenantId == tenantId && !x.Dispatched && x.RetryCount == 0);
    }

    [Fact]
    public void Stage_EmptyEvents_StagesNothing()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        var staged = OutboxStager.Stage([], tenantId, Occurred, Guid.NewGuid);

        // Assert
        staged.Should().BeEmpty();
    }

    [Fact]
    public void Stage_EmptyTenant_ThrowsInvalidOperation()
    {
        // Arrange
        var @event = new WidgetCreatedEvent(Guid.NewGuid(), "gadget");

        // Act
        var act = () => OutboxStager.Stage([@event], Guid.Empty, Occurred, Guid.NewGuid);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*tenant*");
    }

    [Theory]
    [MemberData(nameof(SecretEvents))]
    public void Stage_EventWithSecretMember_ThrowsValidationException(IDomainEvent @event, string member)
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        var act = () => OutboxStager.Stage([@event], tenantId, Occurred, Guid.NewGuid);

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage($"*{member}*");
    }

    public static TheoryData<IDomainEvent, string> SecretEvents() => new()
    {
        { new PasswordChangedEvent(Guid.NewGuid(), "s3cret"), "Password" },
        { new ConnectionOpenedEvent(Guid.NewGuid(), "Host=db;Password=x"), "ConnectionString" },
    };

    [Fact]
    public void Stage_OversizedPayload_ThrowsValidationExceptionWithClearError()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var @event = new HugeNoteEvent(new string('n', OutboxStager.MaxPayloadLength + 1));

        // Act
        var act = () => OutboxStager.Stage([@event], tenantId, Occurred, Guid.NewGuid);

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage($"*{OutboxStager.MaxPayloadLength}*");
    }

    [Fact]
    public void ApplyUndispatched_ReturnsOldestFirst_WithBoundedBatch()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var rows = new[]
        {
            Row(tenantId, Occurred.AddHours(3)),
            Row(tenantId, Occurred.AddHours(1)),
            Row(tenantId, Occurred.AddHours(2), dispatched: true),
            Row(tenantId, Occurred.AddHours(2)),
        }.AsQueryable();

        // Act
        var page = OutboxStager.ApplyUndispatched(rows, 2).ToList();

        // Assert — dispatched rows excluded, oldest first, batch honored.
        page.Should().HaveCount(2);
        page.Select(x => x.OccurredOnUtc).Should().BeInAscendingOrder();
        page.Should().OnlyContain(x => !x.Dispatched);
        page.First().OccurredOnUtc.Should().Be(Occurred.AddHours(1));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-10, 1)]
    [InlineData(int.MaxValue, OutboxStager.MaxBatchSize)]
    public void ApplyUndispatched_ClampsBatchSize(int requested, int expectedTake)
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var rows = Enumerable.Range(0, OutboxStager.MaxBatchSize + 10)
            .Select(i => Row(tenantId, Occurred.AddMinutes(i)))
            .AsQueryable();

        // Act
        var page = OutboxStager.ApplyUndispatched(rows, requested).ToList();

        // Assert
        page.Should().HaveCount(expectedTake);
    }

    private static OutboxMessage Row(Guid tenantId, DateTime occurred, bool dispatched = false) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        IdempotencyKey = Guid.NewGuid().ToString("N"),
        Type = typeof(WidgetCreatedEvent).AssemblyQualifiedName!,
        Payload = "{}",
        OccurredOnUtc = occurred,
        Dispatched = dispatched,
        RetryCount = 0,
    };
}
