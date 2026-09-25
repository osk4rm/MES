using AsistOff.MES.Multitenancy.Context;
using AsistOff.MES.Production.Application.Telemetry;
using AsistOff.MES.Shared.Infrastructure.Interceptors;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using AsistOff.MES.Shared.Infrastructure.Protection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AsistOff.MES.Integration.Tests.Infrastructure;

/// <summary>
/// Boots the real Gateway host (<see cref="Program"/>) against a Testcontainers
/// PostgreSQL database. The application starts exactly as it does locally
/// (Development environment, dev seeder, appsettings.Development.json) - the
/// only changes are that both EF Core contexts are pointed at the container,
/// the telemetry simulator poller is disabled so background writes can
/// never make endpoint assertions flaky, and the abuse-protection throttle
/// budgets are raised so the shared suite can never trip the limiter
/// (isolated 429 tests opt back into tiny budgets via configureProtection).
/// An optional <c>configOverrides</c> dictionary adds in-memory configuration
/// values (e.g. <c>Observability:PrometheusEnabled=true</c>) so isolated hosts
/// can exercise configuration-gated endpoints without touching shared state.
/// </summary>
public sealed class MesWebApplicationFactory(
    string connectionString,
    Action<AbuseProtectionOptions>? configureProtection = null,
    IDictionary<string, string?>? configOverrides = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.UseContentRoot(ResolveContentRoot());

        if (configOverrides is not null)
        {
            // Added last so test overrides win over appsettings files; this
            // is what lets isolated observability hosts enable /metrics or
            // point OTLP at a dummy collector for a single test class.
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(configOverrides));
        }

        builder.ConfigureTestServices(services =>
        {
            RemoveDbContextRegistrations<DefaultContext>(services);
            RemoveDbContextRegistrations<MultitenancyDbContext>(services);

            services.AddDbContext<DefaultContext>(options => options.UseNpgsql(connectionString));
            services.AddDbContext<MultitenancyDbContext>((sp, options) =>
            {
                options.UseNpgsql(connectionString);
                options.AddInterceptors(
                    sp.GetRequiredService<PublishDomainEventsInterceptor>(),
                    sp.GetRequiredService<AuditableEntityInterceptor>());
            });

            // Runs after the production Telemetry section binding, so the
            // poller stays off for the whole integration run.
            services.Configure<TelemetryOptions>(options => options.SimulatorEnabled = false);

            // Same for the OPC UA poller: LastSeenAtUtc must only change via
            // explicit test actions, otherwise connection-status assertions
            // (live/stale, never-seen) would be timing dependent.
            services.Configure<OpcUaPollingOptions>(options => options.PollingEnabled = false);

            // Abuse protection: the shared suite performs hundreds of sign-in
            // and tenant-create calls from a single TestServer IP, which would
            // trip the production budgets (100 sign-ins / 60 creates per
            // minute). Raise them to a level the suite can never reach; hosts
            // needing tiny budgets (the 429 tests) pass configureProtection,
            // which runs after this default so it wins for that host only.
            services.Configure<AbuseProtectionOptions>(options =>
            {
                options.SignIn.PermitLimit = 10000;
                options.SignIn.WindowSeconds = 60;
                options.TenantCreate.PermitLimit = 10000;
                options.TenantCreate.WindowSeconds = 60;
            });

            // Abuse-protection overrides (e.g. tiny throttle budgets for the
            // 429 tests) run after the default above, so the values set here
            // win for this host only.
            if (configureProtection is not null)
            {
                services.Configure(configureProtection);
            }
        });
    }

    /// <summary>
    /// Removes the options descriptors previously registered by the module's
    /// <c>AddDbContext</c> call so the test connection string takes precedence.
    /// </summary>
    private static void RemoveDbContextRegistrations<TContext>(IServiceCollection services)
        where TContext : DbContext
    {
        var optionsType = typeof(DbContextOptions<TContext>);
        var descriptorsToRemove = services.Where(descriptor =>
        {
            var serviceType = descriptor.ServiceType;
            if (serviceType == optionsType || serviceType == typeof(DbContextOptions))
            {
                return true;
            }

            return serviceType.IsGenericType
                   && serviceType.GetGenericArguments().Length == 1
                   && serviceType.GetGenericArguments()[0] == typeof(TContext)
                   && (serviceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>)
                       || serviceType.Name == "IDbContextOptionsConfiguration`1");
        }).ToList();

        foreach (var descriptor in descriptorsToRemove)
        {
            services.Remove(descriptor);
        }
    }

    /// <summary>
    /// Resolves the Gateway project directory (which holds appsettings.json)
    /// from the test output folder: bin/&lt;Config&gt;/net10.0 -> repository root -> Gateway.
    /// </summary>
    private static string ResolveContentRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 5 && directory.Parent is not null; i++)
        {
            directory = directory.Parent;
        }

        var gatewayPath = Path.Combine(directory.FullName, "AsistOff.MES.Gateway");
        return Directory.Exists(gatewayPath) ? gatewayPath : directory.FullName;
    }
}
