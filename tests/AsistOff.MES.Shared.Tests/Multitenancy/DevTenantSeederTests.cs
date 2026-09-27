using AsistOff.MES.Multitenancy.Context;
using AsistOff.MES.Multitenancy.Contracts;
using AsistOff.MES.Multitenancy.Entity;
using AsistOff.MES.Multitenancy.Requests.Commands.Create;
using AsistOff.MES.Multitenancy.Seeding;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AsistOff.MES.Shared.Tests.Multitenancy;

/// <summary>
/// Fail-closed dev seeding (issue #358). The shipped
/// <c>appsettings.Development.json</c> carries an empty seed
/// <c>AdminPassword</c>, so <see cref="DevTenantSeeder"/> must skip tenants
/// with no explicit password (logging a warning and sending nothing) while
/// still provisioning tenants that carry an explicit password supplied via
/// <c>Seed__Tenants__0__AdminPassword</c> or user secrets.
/// </summary>
public sealed class DevTenantSeederTests
{
    [Fact]
    public async Task Seed_EmptyAdminPassword_SkipsTenantAndLogsWarningAsync()
    {
        // Arrange — the shipped default: empty password, Development boot.
        var db = CreateDb();
        var mediator = new Mock<ISender>();
        var logger = new CapturingLogger();
        var seeder = CreateSeeder(db, mediator.Object, logger, "Development", new DevTenantSeed
        {
            Name = "dev",
            DisplayName = "Dev Tenant",
            ContactEmail = "admin@dev.local",
            AdminPassword = string.Empty,
        });

        // Act
        await seeder.Seed();

        // Assert — fail-closed: nothing sent, one skip warning, no tenant row.
        mediator.Verify(
            m => m.Send(It.IsAny<CreateTenantCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        logger.Entries.Should().ContainSingle()
            .Which.Should().Match<(LogLevel Level, string Message)>(
                e => e.Level == LogLevel.Warning && e.Message.Contains("Skipping dev tenant seed"));
        (await db.Tenants.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Seed_ExplicitPassword_SendsCreateTenantCommandAsync()
    {
        // Arrange — explicit local-dev override present.
        var db = CreateDb();
        var mediator = new Mock<ISender>();
        mediator.Setup(m => m.Send(It.IsAny<CreateTenantCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnonymousTenantResponse(Guid.NewGuid(), "dev", true));
        var logger = new CapturingLogger();
        var seeder = CreateSeeder(db, mediator.Object, logger, "Development", new DevTenantSeed
        {
            Name = "dev",
            DisplayName = "Dev Tenant",
            ContactEmail = "admin@dev.local",
            AdminPassword = "S0mething-Strong!",
        });

        // Act
        await seeder.Seed();

        // Assert — the exact self-service pipeline command is issued once.
        mediator.Verify(
            m => m.Send(
                It.Is<CreateTenantCommand>(c =>
                    c.Name == "dev" &&
                    c.ContactEmail == "admin@dev.local" &&
                    c.Password == "S0mething-Strong!" &&
                    c.ConfirmPassword == "S0mething-Strong!"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        logger.Entries.Should().NotContain(
            e => e.Level == LogLevel.Warning && e.Message.Contains("Skipping dev tenant seed"));
    }

    [Fact]
    public async Task Seed_NonDevelopmentEnvironment_SendsNothingAsync()
    {
        // Arrange — the pre-existing Development-only guard still holds.
        var db = CreateDb();
        var mediator = new Mock<ISender>();
        var logger = new CapturingLogger();
        var seeder = CreateSeeder(db, mediator.Object, logger, "Production", new DevTenantSeed
        {
            Name = "dev",
            DisplayName = "Dev Tenant",
            ContactEmail = "admin@dev.local",
            AdminPassword = "S0mething-Strong!",
        });

        // Act
        await seeder.Seed();

        // Assert
        mediator.Verify(
            m => m.Send(It.IsAny<CreateTenantCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Seed_ExistingTenant_SkipsWithoutSendAsync()
    {
        // Arrange — idempotency: the tenant row already exists.
        var db = CreateDb();
        db.Tenants.Add(new Tenant { Name = "dev", DisplayName = "Dev Tenant", ContactEmail = "admin@dev.local" });
        await db.SaveChangesAsync();
        var mediator = new Mock<ISender>();
        var logger = new CapturingLogger();
        var seeder = CreateSeeder(db, mediator.Object, logger, "Development", new DevTenantSeed
        {
            Name = "dev",
            DisplayName = "Dev Tenant",
            ContactEmail = "admin@dev.local",
            AdminPassword = "S0mething-Strong!",
        });

        // Act
        await seeder.Seed();

        // Assert
        mediator.Verify(
            m => m.Send(It.IsAny<CreateTenantCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static DevTenantSeeder CreateSeeder(
        MultitenancyDbContext db,
        ISender mediator,
        ILogger<DevTenantSeeder> logger,
        string environmentName,
        params DevTenantSeed[] tenants)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(environmentName);
        var options = Options.Create(new DevTenantSeedOptions
        {
            Enabled = true,
            Tenants = tenants.ToList(),
        });

        return new DevTenantSeeder(environment.Object, options, db, mediator, logger);
    }

    private static MultitenancyDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MultitenancyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new MultitenancyDbContext(options);
    }

    /// <summary>
    /// Hand-rolled capturing logger: Moq cannot proxy
    /// <c>ILogger&lt;T&gt;</c> when <c>T</c> is internal (DynamicProxyGenAssembly2
    /// would need its own InternalsVisibleTo), so the tests record entries
    /// directly instead of verifying a mock.
    /// </summary>
    private sealed class CapturingLogger : ILogger<DevTenantSeeder>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        IDisposable? ILogger.BeginScope<TState>(TState state) => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
