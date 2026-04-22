using AsistOff.MES.Configuration.Application.Features.Warehouses.Browse;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Configuration.Infrastructure.DAL;
using AsistOff.MES.Configuration.Infrastructure.Repositories;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Configuration.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddPostgres<ConfigurationDbContext>();
        services.AddScoped<IWarehousesRepository, WarehousesRepository>();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(BrowseWarehousesRequestHandler).Assembly));

        return services;
    }
}