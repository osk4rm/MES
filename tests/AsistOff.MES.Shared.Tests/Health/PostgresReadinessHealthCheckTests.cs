using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Shared.Infrastructure.Health;
using AsistOff.MES.Shared.Infrastructure.Interceptors;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;

namespace AsistOff.MES.Shared.Tests.Health;

/// <summary>
/// Verifies the PostgreSQL readiness check backing <c>/health/ready</c>:
/// healthy when the database answers, unhealthy (without leaking connection
/// details) when it does not — issue #249.
/// </summary>
public class PostgresReadinessHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_DatabaseReachable_ReturnsHealthy()
    {
        // Arrange
        var scopeFactory = ScopeFactory(options =>
            options.UseInMemoryDatabase($"health-ready-{Guid.NewGuid():N}"));
        var check = new PostgresReadinessHealthCheck(scopeFactory);

        // Act
        var result = await check.CheckHealthAsync(Context(check));

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_DatabaseUnreachable_ReturnsUnhealthyWithoutLeak()
    {
        // Arrange - port 1 refuses immediately on loopback, so the check fails
        // fast without depending on network timeouts.
        const string secretFragment = "mes-health-secret-249";
        var scopeFactory = ScopeFactory(options => options.UseNpgsql(
            $"Host=127.0.0.1;Port=1;Database=mes;Username=mes;Password={secretFragment};Timeout=2;Command Timeout=2"));
        var check = new PostgresReadinessHealthCheck(scopeFactory);

        // Act
        var result = await check.CheckHealthAsync(Context(check));

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Exception.Should().BeNull("provider error details must never travel with the result");
        result.Description.Should().Be(PostgresReadinessHealthCheck.UnavailableDescription);
        result.Description.Should().NotContain(secretFragment);
        result.Description.Should().NotContain("Password");
    }

    private static HealthCheckContext Context(IHealthCheck check)
        => new()
        {
            Registration = new HealthCheckRegistration(
                HealthProbes.PostgresCheckName, check, null, new[] { HealthProbes.ReadyTag })
        };

    private static IServiceScopeFactory ScopeFactory(Action<DbContextOptionsBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IPublisher>());
        services.AddSingleton(Mock.Of<IDateTimeProvider>());
        services.AddSingleton(Mock.Of<ICurrentUserAccessor>());
        services.AddSingleton(Mock.Of<ICurrentTenantAccessor>());
        services.AddSingleton(Mock.Of<IGuidProvider>());
        services.AddScoped<PublishDomainEventsInterceptor>();
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<AuditHistoryInterceptor>();
        services.AddScoped<SaasyEntityInterceptor>();
        services.AddDbContext<DefaultContext>(configure);

        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }
}
