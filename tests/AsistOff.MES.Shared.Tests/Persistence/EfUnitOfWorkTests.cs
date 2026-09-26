using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Shared.Infrastructure.Interceptors;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AsistOff.MES.Shared.Tests.Persistence;

/// <summary>
/// Direct transaction semantics for <see cref="EfUnitOfWork"/> (issue #265, AC2):
/// all repository <c>SaveChangesAsync</c> calls inside
/// <c>ExecuteInTransactionAsync</c> commit together on success and roll back
/// together when the callback throws. Uses SQLite in-memory (relational, so
/// real BEGIN/COMMIT/ROLLBACK) rather than the InMemory provider, which does
/// not support transactions.
/// </summary>
public sealed class EfUnitOfWorkTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DefaultContext _context;
    private readonly EfUnitOfWork _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public EfUnitOfWorkTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var tenantAccessor = new Mock<ICurrentTenantAccessor>();
        var capturedTenant = _tenantId;
        tenantAccessor.Setup(a => a.TryGetTenantId(out capturedTenant)).Returns(true);
        tenantAccessor.SetupGet(a => a.CurrentTenantId).Returns(_tenantId);

        var userAccessor = new Mock<ICurrentUserAccessor>();
        userAccessor.SetupGet(a => a.UserId).Returns((Guid?)null);

        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc));

        var guids = new Mock<IGuidProvider>();
        guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());

        var mediator = new Mock<IPublisher>();

        var options = new DbContextOptionsBuilder<DefaultContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new DefaultContext(
            options,
            Array.Empty<IEntityConfigurator>(),
            new PublishDomainEventsInterceptor(mediator.Object, clock.Object, guids.Object, tenantAccessor.Object),
            new AuditableEntityInterceptor(clock.Object, userAccessor.Object),
            new AuditHistoryInterceptor(clock.Object, userAccessor.Object, tenantAccessor.Object, guids.Object),
            new SaasyEntityInterceptor(tenantAccessor.Object),
            tenantAccessor.Object);

        _context.Database.EnsureCreated();

        _sut = new EfUnitOfWork(_context);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_CommitsAllWrites_OnSuccess()
    {
        // Arrange
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        // Act
        var result = await _sut.ExecuteInTransactionAsync(async () =>
        {
            _context.Set<Warehouse>().Add(new Warehouse { Id = firstId, TenantId = _tenantId, Name = "WH-1" });
            _context.Set<Warehouse>().Add(new Warehouse { Id = secondId, TenantId = _tenantId, Name = "WH-2" });
            await _context.SaveChangesAsync();
            return "ok";
        });

        // Assert
        result.Should().Be("ok");
        _context.ChangeTracker.Clear();
        var count = await _context.Set<Warehouse>().IgnoreQueryFilters().CountAsync();
        count.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_RollsBackAllWrites_AndRethrows_OnFailure()
    {
        // Arrange
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        // Act
        var act = () => _sut.ExecuteInTransactionAsync<string>(async () =>
        {
            _context.Set<Warehouse>().Add(new Warehouse { Id = firstId, TenantId = _tenantId, Name = "WH-1" });
            _context.Set<Warehouse>().Add(new Warehouse { Id = secondId, TenantId = _tenantId, Name = "WH-2" });
            await _context.SaveChangesAsync();
            throw new InvalidOperationException("simulated mid-fan-out failure");
        });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("simulated mid-fan-out failure");
        _context.ChangeTracker.Clear();
        var count = await _context.Set<Warehouse>().IgnoreQueryFilters().CountAsync();
        count.Should().Be(0, "a mid-fan-out failure must roll back every write in the transaction");
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
