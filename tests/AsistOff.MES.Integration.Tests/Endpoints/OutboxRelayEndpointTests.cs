using System.Text.Json;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.Outbox;
using AsistOff.MES.Multitenancy.Context;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Models.DomainEvents;
using AsistOff.MES.Shared.Infrastructure.Outbox;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using AsistOff.MES.Shared.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Slice 2 (#259) relay coverage against the real host and a real PostgreSQL.
/// The relay timer loop is disabled in the test host (see
/// <see cref="MesWebApplicationFactory"/>) so background cycles can never make
/// assertions flaky; tests drive the relay explicitly per tenant via
/// <c>RelayTenantAsync</c>, which also proves tenant isolation: relaying one
/// tenant never touches another tenant's rows. Handler retry goes through the
/// real host MediatR pipeline via the test-only <c>FlakyOutboxHandler</c>
/// (registered in the factory, firing exclusively for its test-only event).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class OutboxRelayEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private sealed record RelayProbeEvent(Guid ReasonCodeId, string Code) : IDomainEvent;

    [Fact]
    public async Task RelayTenant_CommittedRow_DispatchesAndMarksDispatched()
    {
        // Arrange — one tenant with exactly one staged row.
        var (email, _) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(email);
        var code = $"RC-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        await SaveReasonCodeWithEventAsync(tenantId, code);

        // Act — relay that tenant explicitly.
        var relay = Fixture.Services.GetRequiredService<OutboxRelayService>();
        var dispatched = await relay.RelayTenantAsync(tenantId);

        // Assert — the committed row was delivered exactly once and marked.
        dispatched.Should().Be(1);

        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();

            // Slice 3 (#260): tenant bootstrap leaves its dispatched
            // tenant-created row; the probe row is matched by payload marker.
            var row = await context.OutboxMessages.AsNoTracking().SingleAsync(x => x.Payload.Contains(code));
            row.TenantId.Should().Be(tenantId);
            row.Type.Should().Contain(nameof(RelayProbeEvent));
            row.Payload.Should().Contain(code);
            row.Dispatched.Should().BeTrue();
        }
    }

    [Fact]
    public async Task RelayTenant_RolledBackSave_DispatchesNothing()
    {
        // Arrange — a save inside an explicit transaction that is rolled back.
        var (email, _) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(email);
        var code = $"RC-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
            await using var transaction = await context.Database.BeginTransactionAsync();

            var reasonCode = NewReasonCode(tenantId, code);
            reasonCode.AddDomainEvent(new RelayProbeEvent(reasonCode.Id, code));
            context.Set<ReasonCode>().Add(reasonCode);
            await context.SaveChangesAsync();

            await transaction.RollbackAsync();
        }

        // Act
        var relay = Fixture.Services.GetRequiredService<OutboxRelayService>();
        var dispatched = await relay.RelayTenantAsync(tenantId);

        // Assert — the rolled-back write left no rows, hence zero dispatches.
        // Slice 3 (#260): tenant bootstrap leaves its dispatched
        // tenant-created row, so only undispatched rows are asserted here.
        dispatched.Should().Be(0);

        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
            (await OutboxStager.ApplyUndispatched(context.OutboxMessages, 100, GetMaxAttempts())
                .AsNoTracking().ToListAsync()).Should().BeEmpty();
        }
    }

    [Fact]
    public async Task RelayTenant_UnknownEventType_ParksPoison()
    {
        // Arrange — a row whose event type can never resolve.
        var (email, _) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(email);
        var maxAttempts = GetMaxAttempts();
        var outboxId = Guid.NewGuid();
        await InsertRawRowAsync(tenantId, outboxId, "Missing.Event, MissingAssembly", """{"code":"X"}""");

        // Act
        var relay = Fixture.Services.GetRequiredService<OutboxRelayService>();
        var dispatched = await relay.RelayTenantAsync(tenantId);

        // Assert — parked, never delivered, and never refetched by the relay.
        dispatched.Should().Be(0);

        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();

            var row = await context.OutboxMessages.AsNoTracking().SingleAsync(x => x.Id == outboxId);
            row.Dispatched.Should().BeFalse();
            row.RetryCount.Should().Be(maxAttempts);

            var relayPage = await OutboxStager
                .ApplyUndispatched(context.OutboxMessages, 100, maxAttempts)
                .ToListAsync();
            relayPage.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task RelayTenant_PoisonedRow_IsSkipped()
    {
        // Arrange — a row already parked at the attempt budget stays parked.
        var (email, _) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(email);
        var maxAttempts = GetMaxAttempts();
        var code = $"RC-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        await SaveReasonCodeWithEventAsync(tenantId, code);

        Guid parkedId;
        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
            // Slice 3 (#260): tenant bootstrap leaves its dispatched
            // tenant-created row; the probe row is the only undispatched one.
            var row = await context.OutboxMessages.SingleAsync(x => !x.Dispatched);
            row.RetryCount = maxAttempts;
            await context.SaveChangesAsync();
            parkedId = row.Id;
        }

        // Act
        var relay = Fixture.Services.GetRequiredService<OutboxRelayService>();
        var dispatched = await relay.RelayTenantAsync(tenantId);

        // Assert — the parked row is skipped untouched.
        dispatched.Should().Be(0);

        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
            var row = await context.OutboxMessages.AsNoTracking().SingleAsync(x => x.Id == parkedId);
            row.Dispatched.Should().BeFalse();
            row.RetryCount.Should().Be(maxAttempts);
        }
    }

    [Fact]
    public async Task RelayTenant_FlakyHandlerFailsOnceThenSucceeds_RetriesAndDispatches()
    {
        // Arrange — a committed row whose host handler fails once, then recovers.
        var (email, _) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(email);
        FlakyOutboxHandler.Reset(failuresRemaining: 1);

        try
        {
            await InsertEventRowAsync(tenantId, new FlakyOutboxEvent(Guid.NewGuid(), "FLAKY"));

            // Act — relay that tenant explicitly through the real host pipeline.
            var relay = Fixture.Services.GetRequiredService<OutboxRelayService>();
            var dispatched = await relay.RelayTenantAsync(tenantId);

            // Assert — retried with backoff through MediatR, then delivered.
            dispatched.Should().Be(1);
            FlakyOutboxHandler.Calls.Should().Be(2);

            using (BackgroundTenantContext.BeginScope(tenantId))
            {
                using var scope = Fixture.Services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();

                // Slice 3 (#260): tenant bootstrap leaves its dispatched
                // tenant-created row; the flaky row is matched by marker.
                var row = await context.OutboxMessages.AsNoTracking()
                    .SingleAsync(x => x.Payload.Contains("FLAKY"));
                row.Dispatched.Should().BeTrue();
                row.RetryCount.Should().Be(1);
            }
        }
        finally
        {
            FlakyOutboxHandler.Reset();
        }
    }

    [Fact]
    public async Task RelayTenant_HandlerFailsOnLastAttempt_ParksPoison()
    {
        // Arrange — a row one attempt short of the budget whose host handler is down.
        var (email, _) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(email);
        var maxAttempts = GetMaxAttempts();
        FlakyOutboxHandler.Reset(alwaysFail: true);

        try
        {
            var outboxId = await InsertEventRowAsync(
                tenantId, new FlakyOutboxEvent(Guid.NewGuid(), "POISON"), retryCount: maxAttempts - 1);

            // Act
            var relay = Fixture.Services.GetRequiredService<OutboxRelayService>();
            var dispatched = await relay.RelayTenantAsync(tenantId);

            // Assert — the final budgeted attempt failed, so the row parks as
            // poison (no backoff wait on the terminal attempt) and is never refetched.
            dispatched.Should().Be(0);
            FlakyOutboxHandler.Calls.Should().Be(1);

            using (BackgroundTenantContext.BeginScope(tenantId))
            {
                using var scope = Fixture.Services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();

                var row = await context.OutboxMessages.AsNoTracking().SingleAsync(x => x.Id == outboxId);
                row.Dispatched.Should().BeFalse();
                row.RetryCount.Should().Be(maxAttempts);

                var relayPage = await OutboxStager
                    .ApplyUndispatched(context.OutboxMessages, 100, maxAttempts)
                    .ToListAsync();
                relayPage.Should().BeEmpty();
            }
        }
        finally
        {
            FlakyOutboxHandler.Reset();
        }
    }

    [Fact]
    public async Task RelayTenant_CrossTenant_DispatchesOnlyTargetTenant()
    {
        // Arrange — two tenants, each with one staged row.
        var (emailA, _) = await Fixture.CreateTenantAsync();
        var tenantA = await GetTenantIdByEmailAsync(emailA);
        var codeA = $"RC-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        await SaveReasonCodeWithEventAsync(tenantA, codeA);

        var (emailB, _) = await Fixture.CreateTenantAsync();
        var tenantB = await GetTenantIdByEmailAsync(emailB);
        var codeB = $"RC-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        await SaveReasonCodeWithEventAsync(tenantB, codeB);

        var relay = Fixture.Services.GetRequiredService<OutboxRelayService>();

        // Act — relay tenant A only.
        var dispatchedA = await relay.RelayTenantAsync(tenantA);

        // Assert — A's probe row dispatched under A; B's probe row untouched.
        // Slice 3 (#260): tenant bootstrap leaves each tenant's dispatched
        // tenant-created row, so probe rows are matched by payload marker.
        dispatchedA.Should().Be(1);

        using (BackgroundTenantContext.BeginScope(tenantA))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
            (await context.OutboxMessages.AsNoTracking().ToListAsync())
                .Should().ContainSingle(x => x.Payload.Contains(codeA))
                .Which.Dispatched.Should().BeTrue();
        }

        using (BackgroundTenantContext.BeginScope(tenantB))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
            (await context.OutboxMessages.AsNoTracking().ToListAsync())
                .Should().ContainSingle(x => x.Payload.Contains(codeB))
                .Which.Dispatched.Should().BeFalse();
        }

        // Act — relay tenant B; its row is delivered under B.
        var dispatchedB = await relay.RelayTenantAsync(tenantB);

        // Assert
        dispatchedB.Should().Be(1);

        using (BackgroundTenantContext.BeginScope(tenantB))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
            var row = await context.OutboxMessages.AsNoTracking()
                .SingleAsync(x => x.Payload.Contains(codeB));
            row.TenantId.Should().Be(tenantB);
            row.Dispatched.Should().BeTrue();
        }
    }

    private int GetMaxAttempts()
    {
        using var scope = Fixture.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IOptions<OutboxRelayOptions>>().Value.MaxAttempts;
    }

    private async Task SaveReasonCodeWithEventAsync(Guid tenantId, string code)
    {
        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();

            var reasonCode = NewReasonCode(tenantId, code);
            reasonCode.AddDomainEvent(new RelayProbeEvent(reasonCode.Id, code));
            context.Set<ReasonCode>().Add(reasonCode);
            await context.SaveChangesAsync();
        }
    }

    private async Task<Guid> InsertEventRowAsync(Guid tenantId, IDomainEvent @event, int retryCount = 0)
    {
        var eventType = @event.GetType();
        var outboxId = Guid.NewGuid();

        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();

            context.OutboxMessages.Add(new OutboxMessage
            {
                Id = outboxId,
                TenantId = tenantId,
                IdempotencyKey = Guid.NewGuid().ToString("N"),
                Type = eventType.AssemblyQualifiedName!,
                Payload = JsonSerializer.Serialize(@event, eventType),
                OccurredOnUtc = DateTime.UtcNow,
                Dispatched = false,
                RetryCount = retryCount,
            });
            await context.SaveChangesAsync();
        }

        return outboxId;
    }

    private async Task InsertRawRowAsync(Guid tenantId, Guid outboxId, string type, string payload)
    {
        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();

            context.OutboxMessages.Add(new OutboxMessage
            {
                Id = outboxId,
                TenantId = tenantId,
                IdempotencyKey = Guid.NewGuid().ToString("N"),
                Type = type,
                Payload = payload,
                OccurredOnUtc = DateTime.UtcNow,
                Dispatched = false,
                RetryCount = 0,
            });
            await context.SaveChangesAsync();
        }
    }

    private static ReasonCode NewReasonCode(Guid tenantId, string code) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Code = code,
        Name = $"Outbox relay probe {code}",
        Description = null,
        Category = ReasonCodeCategory.Downtime,
        IsActive = true,
        SortIndex = 0,
    };

    private async Task<Guid> GetTenantIdByEmailAsync(string email)
    {
        using var scope = Fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MultitenancyDbContext>();
        var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.ContactEmail == email);
        return tenant.Id;
    }
}
