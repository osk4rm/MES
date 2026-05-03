using AsistOff.MES.CustomerOrders.Domain.Repositories;
using AsistOff.MES.CustomerOrders.Infrastructure.Configurations;
using AsistOff.MES.CustomerOrders.Infrastructure.Repositories;
using AsistOff.MES.Shared.Abstractions.DAL;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.CustomerOrders.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCustomerOrdersInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ICustomersRepository, CustomersRepository>();
        services.AddScoped<ICustomerOrdersRepository, CustomerOrdersRepository>();
        services.AddScoped<ICustomerOrderLinesRepository, CustomerOrderLinesRepository>();
        services.AddScoped<IEntityConfigurator, CustomerOrdersEntityConfigurator>();
        return services;
    }
}
