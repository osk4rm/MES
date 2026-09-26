using System.Text.Json;
using AsistOff.MES.Multitenancy.Contracts.Events;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Multitenancy.Entity;
using AsistOff.MES.Multitenancy.Outbox;
using AsistOff.MES.Multitenancy.Requests.Commands.Create;
using AsistOff.MES.Multitenancy.Repositories;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Shared.Infrastructure.Interceptors;
using AsistOff.MES.Shared.Infrastructure.Outbox;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using AsistOff.MES.Users.Core.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Multitenancy;

/// <summary>
/// Slice 3 (#260) cutover coverage: the tenant handler commits the tenant row
/// first and stages the tenant-created event through the outbox (never via a
/// direct inline publish), so a failed tenant save stages nothing and
/// dispatches zero events; the writer maps one event to one undispatched row
/// bound to the created tenant id, rejects staging without a tenant, and the
/// payload carries the password hash but never plaintext.
/// </summary>
public class TenantCreatedOutboxTests
{
    private readonly Mock<ITenantRepository> _repository = new();
    private readonly Mock<ITenantCreatedEventOutbox> _outbox = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IPasswordHasher<User>> _hasher = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 26, 10, 0, 0, DateTimeKind.Utc);

    public TenantCreatedOutboxTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(_tenantId);
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _hasher.Setup(h => h.HashPassword(It.IsAny<User>(), It.IsAny<string>()))
            .Returns("HASHED");
    }

    private CreateTenantCommandHandler CreateHandler() =>
        new(_repository.Object, _guids.Object, _clock.Object,
            NullLogger<CreateTenantCommandHandler>.Instance, _outbox.Object, _hasher.Object);

    private static CreateTenantCommand ValidCommand() => new(
        "acme", "Acme Corp", "admin@acme.local", string.Empty, "Passw0rd!", "Passw0rd!");

    [Fact]
    public async Task Handle_ValidCommand_CreatesTenant_ThenStagesEventForCreatedTenant()
    {
        // Arrange
        var order = new List<string>();
        _repository.Setup(r => r.CreateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("create"))
            .Returns(Task.CompletedTask);
        _outbox.Setup(o => o.StageAsync(It.IsAny<TenantCreatedEvent>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("stage"))
            .Returns(Task.CompletedTask);

        // Act
        var result = await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        // Assert — tenant row first, event staged after; no inline publish exists.
        result.Id.Should().Be(_tenantId);
        order.Should().Equal("create", "stage");
        _outbox.Verify(o => o.StageAsync(
            It.Is<TenantCreatedEvent>(e => e.Id == _tenantId && e.Email == "admin@acme.local" && e.HashedPassword == "HASHED"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TenantCreateFails_StagesNothing_AndThrowsRepositoryException()
    {
        // Arrange — the tenant save fails (e.g. duplicate name): the attempt
        // rolls back before staging is ever reached.
        _repository.Setup(r => r.CreateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("duplicate name"));

        // Act
        var act = () => CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        // Assert — zero staged rows, zero dispatches for the failed attempt.
        await act.Should().ThrowAsync<RepositoryException>();
        _outbox.Verify(o => o.StageAsync(It.IsAny<TenantCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_StagingFails_ThrowsRepositoryException()
    {
        // Arrange — tenant committed, outbox staging failed afterwards.
        _repository.Setup(r => r.CreateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _outbox.Setup(o => o.StageAsync(It.IsAny<TenantCreatedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Payload", "oversized"));

        // Act
        var act = () => CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<RepositoryException>();
    }

    private sealed class AnonymousTenantAccessor : ICurrentTenantAccessor
    {
        public Guid CurrentTenantId => Guid.Empty;

        public bool TryGetTenantId(out Guid tenantId)
        {
            tenantId = Guid.Empty;
            return false;
        }
    }

    private sealed class AnonymousUserAccessor : ICurrentUserAccessor
    {
        public Guid? UserId => null;
    }

    private sealed class TestClock(DateTime now) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = now;
    }

    private sealed class TestGuids : IGuidProvider
    {
        public Guid NewGuid() => Guid.NewGuid();
    }

    private sealed class AmbientTenantAccessor(Guid ambientTenantId) : ICurrentTenantAccessor
    {
        // Production-like accessor (mirrors TenantContext): an explicit
        // BackgroundTenantContext scope wins over the ambient HTTP tenant, so
        // the writer's scope for the created tenant satisfies the
        // SaasyEntityInterceptor even when the caller is authenticated as a
        // different tenant (e.g. /register submitted while logged in).
        public Guid CurrentTenantId => TryGetTenantId(out var tenantId) ? tenantId : Guid.Empty;

        public bool TryGetTenantId(out Guid tenantId)
        {
            var background = BackgroundTenantContext.Current;
            if (background.HasValue && background.Value != Guid.Empty)
            {
                tenantId = background.Value;
                return true;
            }

            tenantId = ambientTenantId;
            return ambientTenantId != Guid.Empty;
        }
    }

    private DefaultContext BuildDefaultContext(DateTime now)
    {
        return BuildDefaultContextWithAccessor(new AnonymousTenantAccessor(), now);
    }

    private DefaultContext BuildDefaultContextWithAccessor(ICurrentTenantAccessor tenants, DateTime now)
    {
        var clock = new TestClock(now);
        var guids = new TestGuids();
        var users = new AnonymousUserAccessor();

        return new DefaultContext(
            new DbContextOptionsBuilder<DefaultContext>()
                .UseInMemoryDatabase($"tenant-outbox-{Guid.NewGuid()}")
                .Options,
            [new SharedOutboxEntityConfigurator(), new SharedAuditEntityConfigurator()],
            new PublishDomainEventsInterceptor(clock, guids, tenants),
            new AuditableEntityInterceptor(clock, users),
            new AuditHistoryInterceptor(clock, users, tenants, guids),
            new SaasyEntityInterceptor(tenants),
            tenants);
    }

    private static TenantCreatedEventOutboxWriter BuildWriter(DefaultContext context, DateTime now) =>
        new(context, new TestGuids(), new TestClock(now));

    [Fact]
    public async Task Stage_ValidEvent_PersistsSingleUndispatchedRowBoundToCreatedTenant()
    {
        // Arrange — anonymous signup: no ambient tenant, binding comes from
        // the created tenant row only.
        using var context = BuildDefaultContext(_now);
        var tenantId = Guid.NewGuid();
        var @event = new TenantCreatedEvent(tenantId, "admin@acme.local", "HASHED");

        // Act
        await BuildWriter(context, _now).StageAsync(@event);

        // Assert
        var rows = await context.OutboxMessages.IgnoreQueryFilters().ToListAsync();
        rows.Should().ContainSingle();
        var row = rows.Single();
        row.TenantId.Should().Be(tenantId);
        row.Type.Should().Contain(nameof(TenantCreatedEvent));
        row.OccurredOnUtc.Should().Be(_now);
        row.Dispatched.Should().BeFalse();
        row.RetryCount.Should().Be(0);
        row.Id.Should().NotBe(Guid.Empty);
        row.IdempotencyKey.Should().NotBeNullOrWhiteSpace();

        var payload = JsonSerializer.Deserialize<TenantCreatedEvent>(row.Payload);
        payload.Should().BeEquivalentTo(@event);
    }

    [Fact]
    public async Task Stage_Payload_CarriesHash_NeverPlaintext()
    {
        // Arrange
        using var context = BuildDefaultContext(_now);
        const string plaintext = "Sup3r-Plaint3xt-Password!";
        var @event = new TenantCreatedEvent(Guid.NewGuid(), "admin@acme.local", "HASHED");

        // Act
        await BuildWriter(context, _now).StageAsync(@event);

        // Assert — the handler hashes before staging, so plaintext can never
        // reach the row; only the one-way hash is stored.
        var row = await context.OutboxMessages.IgnoreQueryFilters().SingleAsync();
        row.Payload.Should().Contain("HASHED");
        row.Payload.Should().NotContain(plaintext);
        _ = plaintext;
    }

    [Fact]
    public async Task Stage_EmptyTenantId_ThrowsValidation_StagesNothing()
    {
        // Arrange
        using var context = BuildDefaultContext(_now);

        // Act
        var act = () => BuildWriter(context, _now)
            .StageAsync(new TenantCreatedEvent(Guid.Empty, "admin@acme.local", "HASHED"));

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        (await context.OutboxMessages.IgnoreQueryFilters().ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Stage_AuthenticatedAmbientTenant_StagesRowForNewTenant()
    {
        // Arrange — E2E regression (PR #282): /register submitted while
        // logged in carries the caller's ambient tenant, which differs from
        // the newly created tenant id. Staging must succeed under that
        // ambient without a cross-tenant write error.
        var ambientTenantId = Guid.NewGuid();
        using var context = BuildDefaultContextWithAccessor(new AmbientTenantAccessor(ambientTenantId), _now);
        var newTenantId = Guid.NewGuid();
        var @event = new TenantCreatedEvent(newTenantId, "new@acme.local", "HASHED");

        // Act
        await BuildWriter(context, _now).StageAsync(@event);

        // Assert — one undispatched row bound to the new tenant, not ambient.
        var rows = await context.OutboxMessages.IgnoreQueryFilters().ToListAsync();
        rows.Should().ContainSingle();
        rows.Single().TenantId.Should().Be(newTenantId);
        rows.Single().TenantId.Should().NotBe(ambientTenantId);
        rows.Single().Dispatched.Should().BeFalse();

        // The writer's background scope must not leak into the caller flow.
        BackgroundTenantContext.Current.Should().BeNull();
    }

    [Fact]
    public async Task Stage_SameEventTwice_StagesDistinctRows()
    {
        // Arrange — staging is at-least-once by key; redelivery safety lives
        // in the idempotent listener, not in a staging dedupe.
        using var context = BuildDefaultContext(_now);
        var writer = BuildWriter(context, _now);
        var @event = new TenantCreatedEvent(Guid.NewGuid(), "admin@acme.local", "HASHED");

        // Act
        await writer.StageAsync(@event);
        await writer.StageAsync(@event);

        // Assert
        var rows = await context.OutboxMessages.IgnoreQueryFilters().ToListAsync();
        rows.Should().HaveCount(2);
        rows.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        rows.Select(x => x.IdempotencyKey).Should().OnlyHaveUniqueItems();
    }
}
