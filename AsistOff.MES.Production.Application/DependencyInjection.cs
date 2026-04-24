using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Production.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddProductionApplication(this IServiceCollection services)
    {
        return services;
    }
}
