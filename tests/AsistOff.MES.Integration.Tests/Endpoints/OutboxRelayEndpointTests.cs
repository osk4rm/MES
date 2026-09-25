using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Integration.Tests.Infrastructure;
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
/// tenant never touches another tenant's rows.
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

            var row = await context.OutboxMessages.AsNoTracking().SingleAsync();
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
        dispatched.Should().Be(0);

        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
            (await context.OutboxMessages.AsNoTracking().ToListAsync()).Should().BeEmpty();
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
            var row = await context.OutboxMessages.SingleAsync();
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
    public async Task RelayTenant_CrossTenant_DispatchesOnlyTargetTenant()
    {
        // Arrange — two tenants, each with one staged row.
        var (emailA, _) = await Fixture.CreateTenantAsync();
        var tenantA = await GetTenantIdByEmailAsync(emailA);
        await SaveReasonCodeWithEventAsync(tenantA, $"RC-{Guid.NewGuid():N}"[..12].ToUpperInvariant());

        var (emailB, _) = await Fixture.CreateTenantAsync();
        var tenantB = await GetTenantIdByEmailAsync(emailB);
        await SaveReasonCodeWithEventAsync(tenantB, $"RC-{Guid.NewGuid():N}"[..12].ToUpperInvariant());

        var relay = Fixture.Services.GetRequiredService<OutboxRelayService>();

        // Act — relay tenant A only.
        var dispatchedA = await relay.RelayTenantAsync(tenantA);

        // Assert — A's row dispatched under A; B's row untouched.
        dispatchedA.Should().Be(1);

        using (BackgroundTenantContext.BeginScope(tenantA))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
            (await context.OutboxMessages.AsNoTracking().ToListAsync())
                .Should().ContainSingle()
                .Which.Dispatched.Should().BeTrue();
        }

        using (BackgroundTenantContext.BeginScope(tenantB))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
            (await context.OutboxMessages.AsNoTracking().ToListAsync())
                .Should().ContainSingle()
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
            var row = await context.OutboxMessages.AsNoTracking().SingleAsync();
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
