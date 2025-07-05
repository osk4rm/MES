using AsistOff.MES.Multitenancy.Behaviors;
using AsistOff.MES.Multitenancy.Context;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Multitenancy;

public static class DependencyInjection
{
    public static IServiceCollection AddMultitenancy(this IServiceCollection services)
    {
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TenantValidationBehavior<,>));
        services.AddScoped<ITenantContext, TenantContext>();

        return services;
    }
}