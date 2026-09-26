using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Shared.Infrastructure.Interceptors;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace AsistOff.MES.Shared.Tests.Persistence;

/// <summary>
/// Real-database coverage for the confirmation fan-out transaction (issue #265,
/// AC2): <see cref="DefaultContextUnitOfWork"/> commits all writes together,
/// rolls everything back when the delegate throws, and reuses the ambient
/// transaction for nested calls. Uses SQLite in-memory (relational, supports
/// real transactions) so no Docker/Testcontainers is needed; the production
/// PostgreSQL path is additionally covered by the integration rollback test.
/// </summary>
public sealed class DefaultContextUnitOfWorkTests
{
    private sealed class UowProbe
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
    }

    private sealed class UowProbeConfigurator : IEntityConfigurator
    {
        public void ConfigureEntities(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UowProbe>(b =>
            {
                b.ToTable("UowProbes");
                b.HasKey(x => x.Id);
                b.Property(x => x.Code).HasMaxLength(64);
            });
        }
    }

    private sealed class FakeTenantAccessor : ICurrentTenantAccessor
    {
        public Guid CurrentTenantId => Guid.Empty;

        public bool TryGetTenantId(out Guid tenantId)
        {
            tenantId = Guid.Empty;
            return false;
        }
    }

    private sealed class FakeUserAccessor : ICurrentUserAccessor
    {
        public Guid? UserId => null;
    }

    private sealed class FakeClock : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class FakeGuids : IGuidProvider
    {
        public Guid NewGuid() => Guid.NewGuid();
    }

    private static DefaultContext BuildContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<DefaultContext>()
            .UseSqlite(connection)
            .Options;

        var tenants = new FakeTenantAccessor();
        var users = new FakeUserAccessor();
        var clock = new FakeClock();
        var guids = new FakeGuids();

        return new DefaultContext(
            options,
            [new UowProbeConfigurator()],
            new PublishDomainEventsInterceptor(clock, guids, tenants),
            new AuditableEntityInterceptor(clock, users),
            new AuditHistoryInterceptor(clock, users, tenants, guids),
            new SaasyEntityInterceptor(tenants),
            tenants);
    }

    private static async Task<SqliteConnection> OpenSharedConnectionAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_CommitsAllWritesTogether()
    {
        // Arrange
        using var connection = await OpenSharedConnectionAsync();
        using (var setup = BuildContext(connection))
            await setup.Database.EnsureCreatedAsync();
        using var context = BuildContext(connection);
        var uow = new DefaultContextUnitOfWork(context);
        var first = new UowProbe { Id = Guid.NewGuid(), Code = "P-1" };
        var second = new UowProbe { Id = Guid.NewGuid(), Code = "P-2" };

        // Act
        await uow.ExecuteInTransactionAsync(async () =>
        {
            context.Set<UowProbe>().Add(first);
            context.Set<UowProbe>().Add(second);
            await context.SaveChangesAsync();
        });

        // Assert — both rows committed together, visible from a fresh context.
        using var verify = BuildContext(connection);
        (await verify.Set<UowProbe>().CountAsync()).Should().Be(2);
        (await verify.Set<UowProbe>().AnyAsync(x => x.Id == first.Id)).Should().BeTrue();
        (await verify.Set<UowProbe>().AnyAsync(x => x.Id == second.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_RollsBackAllWritesOnFailure()
    {
        // Arrange
        using var connection = await OpenSharedConnectionAsync();
        using (var setup = BuildContext(connection))
            await setup.Database.EnsureCreatedAsync();
        using var context = BuildContext(connection);
        var uow = new DefaultContextUnitOfWork(context);
        var staged = new UowProbe { Id = Guid.NewGuid(), Code = "ORPHAN" };

        // Act — the confirmation row is staged, then the fan-out fails
        // (movement/edge insert failure in production).
        var act = () => uow.ExecuteInTransactionAsync(async () =>
        {
            context.Set<UowProbe>().Add(staged);
            await context.SaveChangesAsync();
            throw new InvalidOperationException("movement insert failed");
        });

        // Assert — the failure propagates and no partial row survives.
        await act.Should().ThrowAsync<InvalidOperationException>();
        using var verify = BuildContext(connection);
        (await verify.Set<UowProbe>().CountAsync()).Should().Be(0);
        (await verify.Set<UowProbe>().AnyAsync(x => x.Id == staged.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_GenericOverload_ReturnsValueAndCommits()
    {
        // Arrange
        using var connection = await OpenSharedConnectionAsync();
        using (var setup = BuildContext(connection))
            await setup.Database.EnsureCreatedAsync();
        using var context = BuildContext(connection);
        var uow = new DefaultContextUnitOfWork(context);
        var probe = new UowProbe { Id = Guid.NewGuid(), Code = "P-VALUE" };

        // Act
        var result = await uow.ExecuteInTransactionAsync(async () =>
        {
            context.Set<UowProbe>().Add(probe);
            await context.SaveChangesAsync();
            return probe.Code;
        });

        // Assert
        result.Should().Be("P-VALUE");
        using var verify = BuildContext(connection);
        (await verify.Set<UowProbe>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_GenericOverload_RollsBackOnFailure()
    {
        // Arrange
        using var connection = await OpenSharedConnectionAsync();
        using (var setup = BuildContext(connection))
            await setup.Database.EnsureCreatedAsync();
        using var context = BuildContext(connection);
        var uow = new DefaultContextUnitOfWork(context);
        var staged = new UowProbe { Id = Guid.NewGuid(), Code = "ORPHAN" };

        // Act
        var act = () => uow.ExecuteInTransactionAsync<Task<int>>(async () =>
        {
            context.Set<UowProbe>().Add(staged);
            await context.SaveChangesAsync();
            throw new InvalidOperationException("edge insert failed");
        });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        using var verify = BuildContext(connection);
        (await verify.Set<UowProbe>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_Nested_ReusesAmbientTransactionAndCommits()
    {
        // Arrange
        using var connection = await OpenSharedConnectionAsync();
        using (var setup = BuildContext(connection))
            await setup.Database.EnsureCreatedAsync();
        using var context = BuildContext(connection);
        var uow = new DefaultContextUnitOfWork(context);
        var outer = new UowProbe { Id = Guid.NewGuid(), Code = "OUTER" };
        var inner = new UowProbe { Id = Guid.NewGuid(), Code = "INNER" };

        // Act — the inner fan-out reuses the ambient transaction instead of
        // opening a savepoint-less nested transaction.
        await uow.ExecuteInTransactionAsync(async () =>
        {
            context.Set<UowProbe>().Add(outer);
            await context.SaveChangesAsync();

            await uow.ExecuteInTransactionAsync(async () =>
            {
                context.Set<UowProbe>().Add(inner);
                await context.SaveChangesAsync();
            });
        });

        // Assert
        using var verify = BuildContext(connection);
        (await verify.Set<UowProbe>().CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_NestedFailure_RollsBackOuterWrites()
    {
        // Arrange
        using var connection = await OpenSharedConnectionAsync();
        using (var setup = BuildContext(connection))
            await setup.Database.EnsureCreatedAsync();
        using var context = BuildContext(connection);
        var uow = new DefaultContextUnitOfWork(context);
        var outer = new UowProbe { Id = Guid.NewGuid(), Code = "OUTER" };

        // Act — the inner write fails; the single ambient transaction rolls
        // back the outer write too, so no orphan survives.
        var act = () => uow.ExecuteInTransactionAsync(async () =>
        {
            context.Set<UowProbe>().Add(outer);
            await context.SaveChangesAsync();

            await uow.ExecuteInTransactionAsync(() =>
                Task.FromException(new InvalidOperationException("inner fan-out failed")));
        });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        using var verify = BuildContext(connection);
        (await verify.Set<UowProbe>().CountAsync()).Should().Be(0);
    }
}
