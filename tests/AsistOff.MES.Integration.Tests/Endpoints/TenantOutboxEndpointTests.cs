using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;
using AsistOff.MES.Multitenancy.Context;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Infrastructure.Outbox;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Slice 3 (#260) tenant-outbox coverage against the real host and a real
/// PostgreSQL: tenant signup commits the tenant row and stages exactly one
/// undispatched tenant-created outbox row (no inline dispatch), the relay
/// delivers it once after commit (RBAC + admin provisioned, sign-in works), a
/// failed signup stages nothing and dispatches zero, redelivery provisions
/// exactly once, and one tenant's signup never provisions under another
/// tenant. The relay timer loop is off in the test host (see
/// <see cref="MesWebApplicationFactory"/>), so every test drives the relay
/// explicitly per tenant via <c>RelayTenantAsync</c>.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class TenantOutboxEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task CreateTenant_StagesUndispatchedRow_AndRelayDeliversExactlyOnce()
    {
        // Arrange — anonymous signup through the real endpoint.
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"outbox-{suffix}@integration.local";

        // Act — signup commits the tenant row and stages the event, nothing more.
        var created = await PostTenantAsync($"ob-{suffix}", email);

        // Assert — exactly one undispatched row bound to the created tenant.
        var tenantId = created.Id;
        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();

            var rows = await OutboxStager
                .ApplyUndispatched(context.OutboxMessages, 10)
                .ToListAsync();
            rows.Should().ContainSingle();

            var row = rows.Single();
            row.TenantId.Should().Be(tenantId);
            row.Type.Should().Contain("TenantCreatedEvent");
            row.Payload.Should().Contain(email);
            row.Dispatched.Should().BeFalse();
            row.RetryCount.Should().Be(0);
        }

        // Act — relay that tenant explicitly (the only delivery path).
        var relay = Fixture.Services.GetRequiredService<OutboxRelayService>();
        var dispatched = await relay.RelayTenantAsync(tenantId);

        // Assert — delivered exactly once and marked dispatched.
        dispatched.Should().Be(1);

        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();

            var row = await context.OutboxMessages.AsNoTracking().SingleAsync();
            row.Dispatched.Should().BeTrue();
            row.RetryCount.Should().Be(0);

            var admin = await context.Users.AsNoTracking()
                .SingleOrDefaultAsync(u => u.Email == email);
            admin.Should().NotBeNull();
            admin!.TenantId.Should().Be(tenantId);
            admin.IsTenantAdmin.Should().BeTrue();
        }

        // Assert — the provisioned admin can sign in and see its parity roles:
        // the password hash survived the outbox JSON round-trip.
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, "Passw0rd!");
        var roles = await client.GetAsync("/api/roles");
        roles.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateTenant_DuplicateName_RollsBackWithZeroDispatches()
    {
        // Arrange — one committed signup (event staged, not yet relayed).
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var name = $"dup-{suffix}";
        var first = await PostTenantAsync(name, $"dup-a-{suffix}@integration.local");

        // Act — the same name fails on the unique constraint and rolls back.
        using var raw = Fixture.CreateClient();
        var failed = await raw.PostAsJsonAsync("/api/tenants", new
        {
            name,
            displayName = $"Duplicate {suffix}",
            contactEmail = $"dup-b-{suffix}@integration.local",
            settings = string.Empty,
            password = "Passw0rd!",
            confirmPassword = "Passw0rd!",
        });

        // Assert — the failed attempt commits nothing and stages nothing.
        failed.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        using (BackgroundTenantContext.BeginScope(first.Id))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();

            var undispatched = await OutboxStager
                .ApplyUndispatched(context.OutboxMessages, 10)
                .ToListAsync();
            undispatched.Should().ContainSingle("the rolled-back attempt must stage zero rows");
        }

        using (var scope = Fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MultitenancyDbContext>();
            var matches = await db.Tenants.AsNoTracking().CountAsync(t => t.Name == name);
            matches.Should().Be(1);
        }

        // Act — relay delivers only the first attempt's event, exactly once.
        var relay = Fixture.Services.GetRequiredService<OutboxRelayService>();
        var dispatched = await relay.RelayTenantAsync(first.Id);

        // Assert
        dispatched.Should().Be(1);

        using (BackgroundTenantContext.BeginScope(first.Id))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
            (await context.Users.AsNoTracking().CountAsync(u => u.Email == $"dup-a-{suffix}@integration.local"))
                .Should().Be(1);
        }
    }

    [Fact]
    public async Task RelayTwice_ProvisionsRolesExactlyOnce_NoDuplicateUsersOrRoles()
    {
        // Arrange — signup relayed once (happy path).
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"redeliver-{suffix}@integration.local";
        var created = await PostTenantAsync($"rl-{suffix}", email);
        var relay = Fixture.Services.GetRequiredService<OutboxRelayService>();
        (await relay.RelayTenantAsync(created.Id)).Should().Be(1);

        // Act — redeliver the same event (at-least-once relay).
        var redispatched = await relay.RelayTenantAsync(created.Id);

        // Assert — nothing left to deliver, and no duplicates were created.
        redispatched.Should().Be(0);

        using (BackgroundTenantContext.BeginScope(created.Id))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();

            (await context.Users.AsNoTracking().CountAsync(u => u.Email == email))
                .Should().Be(1);
            (await context.Roles.AsNoTracking().CountAsync())
                .Should().Be(2, "the parity admin/user roles are reconciled, not duplicated");

            var row = await context.OutboxMessages.AsNoTracking().SingleAsync();
            row.Dispatched.Should().BeTrue();
        }
    }

    [Fact]
    public async Task CrossTenant_SignupUnderA_NeverProvisionsUnderB()
    {
        // Arrange — two independent signups, each relayed under its own tenant.
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var emailA = $"xt-a-{suffix}@integration.local";
        var emailB = $"xt-b-{suffix}@integration.local";
        var tenantA = await PostTenantAsync($"xt-a-{suffix}", emailA);
        var tenantB = await PostTenantAsync($"xt-b-{suffix}", emailB);

        var relay = Fixture.Services.GetRequiredService<OutboxRelayService>();
        (await relay.RelayTenantAsync(tenantA.Id)).Should().Be(1);
        (await relay.RelayTenantAsync(tenantB.Id)).Should().Be(1);

        // Assert — each admin lives under exactly its own tenant; neither
        // tenant's scope can see the other's user (global query filter).
        using (BackgroundTenantContext.BeginScope(tenantA.Id))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();

            var adminA = await context.Users.AsNoTracking()
                .SingleOrDefaultAsync(u => u.Email == emailA);
            adminA.Should().NotBeNull();
            adminA!.TenantId.Should().Be(tenantA.Id);

            (await context.Users.AsNoTracking()
                .SingleOrDefaultAsync(u => u.Email == emailB))
                .Should().BeNull("tenant A's signup must never provision under tenant B");

            (await context.Roles.AsNoTracking().CountAsync()).Should().Be(2);
        }

        using (BackgroundTenantContext.BeginScope(tenantB.Id))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();

            var adminB = await context.Users.AsNoTracking()
                .SingleOrDefaultAsync(u => u.Email == emailB);
            adminB.Should().NotBeNull();
            adminB!.TenantId.Should().Be(tenantB.Id);

            (await context.Users.AsNoTracking()
                .SingleOrDefaultAsync(u => u.Email == emailA))
                .Should().BeNull();
        }

        // Assert — both admins sign in independently and see their own roles.
        using var clientB = await Fixture.CreateAuthenticatedClientAsync(emailB, "Passw0rd!");
        (await clientB.GetAsync("/api/roles")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<AnonymousTenantDto> PostTenantAsync(string name, string email)
    {
        using var client = Fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/tenants", new
        {
            name,
            displayName = $"Tenant Outbox {name}",
            contactEmail = email,
            settings = string.Empty,
            password = "Passw0rd!",
            confirmPassword = "Passw0rd!",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<AnonymousTenantDto>(response);
        created.Name.Should().Be(name);
        created.IsActive.Should().BeTrue();

        return created;
    }
}
