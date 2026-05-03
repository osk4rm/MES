using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.CustomerOrders.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCustomerOrdersApplication(this IServiceCollection services)
    {
        return services;
    }
}
