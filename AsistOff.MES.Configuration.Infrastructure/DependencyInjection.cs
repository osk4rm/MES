using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Configuration.Infrastructure.Configurations;
using AsistOff.MES.Configuration.Infrastructure.Repositories;
using AsistOff.MES.Shared.Abstractions.DAL;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Configuration.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddConfigurationInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IWarehousesRepository, WarehousesRepository>();
        services.AddScoped<IOperatorsRepository, OperatorsRepository>();
        services.AddScoped<IDepartmentsRepository, DepartmentsRepository>();
        
        services.AddScoped<IEntityConfigurator, ConfigurationEntityConfigurator>();
        
        return services;
    }
}