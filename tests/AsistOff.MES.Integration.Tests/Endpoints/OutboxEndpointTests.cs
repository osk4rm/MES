using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Multitenancy.Context;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Models.DomainEvents;
using AsistOff.MES.Shared.Infrastructure.Outbox;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Slice 1 (#258) outbox write-path coverage against the real host and a real
/// PostgreSQL: saving a tenant-scoped entity carrying a domain event stages
/// exactly one undispatched outbox row (type, payload, tenant) in the same
/// transaction; a rolled-back save leaves no staged rows; cross-tenant reads
/// cannot see other tenants' rows; a rejected HTTP write stages no ghost rows.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class OutboxEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private sealed record ReasonCodeProbeEvent(Guid ReasonCodeId, string Code) : IDomainEvent;

    [Fact]
    public async Task Save_EntityWithDomainEvent_StagesSingleUndispatchedRow()
    {
        // Arrange — isolated tenant with an unambiguous outbox.
        var (email, password) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(email);
        var marker = $"OUTBOX-{Guid.NewGuid():N}"[..14].ToUpperInvariant();

        // Act — save a tenant-scoped entity carrying one domain event.
        await SaveReasonCodeWithEventAsync(tenantId, marker);

        // Assert — exactly one undispatched row with type, payload and tenant.
        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();

            var rows = await OutboxStager.ApplyUndispatched(context.OutboxMessages, 10).ToListAsync();
            rows.Should().ContainSingle();

            var row = rows.Single();
            row.TenantId.Should().Be(tenantId);
            row.Type.Should().Contain(nameof(ReasonCodeProbeEvent));
            row.Payload.Should().Contain(marker);
            row.Dispatched.Should().BeFalse();
            row.RetryCount.Should().Be(0);
            row.Id.Should().NotBe(Guid.Empty);
            row.IdempotencyKey.Should().NotBeNullOrWhiteSpace();
            row.OccurredOnUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
        }

        _ = password;
    }

    [Fact]
    public async Task FailedSave_RollsBackStagedRows()
    {
        // Arrange — one committed entity+event, hence exactly one outbox row.
        var (email, _) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(email);
        var code = $"RC-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        await SaveReasonCodeWithEventAsync(tenantId, code, code);
        var failedMarker = $"FAILED-{Guid.NewGuid():N}"[..14].ToUpperInvariant();

        // Act — a duplicate-code save fails inside an explicit transaction and
        // is rolled back.
        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
            await using var transaction = await context.Database.BeginTransactionAsync();

            var duplicate = NewReasonCode(tenantId, code);
            duplicate.AddDomainEvent(new ReasonCodeProbeEvent(duplicate.Id, failedMarker));
            context.Set<ReasonCode>().Add(duplicate);

            var act = () => context.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>();

            await transaction.RollbackAsync();
        }

        // Assert — the committed row survives, the rolled-back write left none.
        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();

            var rows = await context.OutboxMessages.ToListAsync();
            rows.Should().ContainSingle();
            rows.Should().NotContain(x => x.Payload.Contains(failedMarker));
        }
    }

    [Fact]
    public async Task CrossTenant_ReadCannotSeeOtherTenantRows()
    {
        // Arrange — an outbox row lives in tenant A.
        var (emailA, _) = await Fixture.CreateTenantAsync();
        var tenantA = await GetTenantIdByEmailAsync(emailA);
        await SaveReasonCodeWithEventAsync(tenantA, $"RC-{Guid.NewGuid():N}"[..12].ToUpperInvariant());

        var (emailB, _) = await Fixture.CreateTenantAsync();
        var tenantB = await GetTenantIdByEmailAsync(emailB);

        // Act — tenant B reads the outbox.
        List<Shared.Infrastructure.Persistence.Entities.OutboxMessage> rowsB;
        using (BackgroundTenantContext.BeginScope(tenantB))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
            rowsB = await OutboxStager.ApplyUndispatched(context.OutboxMessages, 10).ToListAsync();
        }

        // Assert — the global query filter hides tenant A rows entirely, while
        // tenant A still sees its own row.
        rowsB.Should().BeEmpty();

        using (BackgroundTenantContext.BeginScope(tenantA))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
            (await context.OutboxMessages.ToListAsync()).Should().ContainSingle();
        }
    }

    [Fact]
    public async Task InvalidHttpWrite_StagesNoGhostRows()
    {
        // Arrange — fresh tenant with an empty outbox.
        var (email, password) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(email);
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);

        // Act — the create fails validation, so nothing is ever saved.
        var failed = await client.PostAsJsonAsync("/api/reason-codes", new
        {
            code = "",
            name = "No code",
            description = (string?)null,
            category = 1,
            isActive = true,
            sortIndex = 0
        });
        failed.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Assert — no ghost outbox row exists for the rejected write.
        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
            (await context.OutboxMessages.ToListAsync()).Should().BeEmpty();
        }
    }

    private async Task SaveReasonCodeWithEventAsync(Guid tenantId, string code, string? payloadMarker = null)
    {
        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();

            var reasonCode = NewReasonCode(tenantId, code);
            reasonCode.AddDomainEvent(new ReasonCodeProbeEvent(reasonCode.Id, payloadMarker ?? code));
            context.Set<ReasonCode>().Add(reasonCode);
            await context.SaveChangesAsync();
        }
    }

    private static ReasonCode NewReasonCode(Guid tenantId, string code) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Code = code,
        Name = $"Outbox probe {code}",
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
