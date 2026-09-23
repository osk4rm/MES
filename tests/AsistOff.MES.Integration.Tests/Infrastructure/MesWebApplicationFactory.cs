using AsistOff.MES.Multitenancy.Context;
using AsistOff.MES.Shared.Infrastructure.Interceptors;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AsistOff.MES.Integration.Tests.Infrastructure;

/// <summary>
/// Boots the real Gateway host (<see cref="Program"/>) against a Testcontainers
/// PostgreSQL database. The application starts exactly as it does locally
/// (Development environment, dev seeder, appsettings.Development.json) - the
/// only change is that both EF Core contexts are pointed at the container.
/// </summary>
public sealed class MesWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.UseContentRoot(ResolveContentRoot());

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
