using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Configuration.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Configuration.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IWarehousesRepository, WarehousesRepository>();
        
        return services;
    }
}