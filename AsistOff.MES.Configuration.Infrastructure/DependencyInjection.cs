using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Configuration.Infrastructure.DAL;
using AsistOff.MES.Configuration.Infrastructure.Repositories;
using AsistOff.MES.Shared.Infrastructure.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Configuration.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IWarehousesRepository, WarehousesRepository>();
        services.AddScoped<IOperatorsRepository, OperatorsRepository>();
        
        services.AddDbContext<ConfigurationDbContext>((sp, options) =>
        {
            var connectionString = configuration["postgres:connectionString"];
            var publishDomainEventsInterceptor = sp.GetRequiredService<PublishDomainEventsInterceptor>();
            options.UseNpgsql(connectionString);
            options.AddInterceptors(publishDomainEventsInterceptor);
        });
        return services;
    }
}