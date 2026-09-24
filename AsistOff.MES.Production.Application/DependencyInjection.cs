using AsistOff.MES.Production.Application.Features.TelemetryReadings;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Production.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddProductionApplication(this IServiceCollection services)
    {
        services.AddScoped<ITelemetryIngestionService, TelemetryIngestionService>();

        return services;
    }
}
