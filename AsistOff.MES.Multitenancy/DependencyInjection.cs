using AsistOff.MES.Multitenancy.Behaviors;
using AsistOff.MES.Multitenancy.Context;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Multitenancy.Entity;
using AsistOff.MES.Multitenancy.Repositories;
using AsistOff.MES.Shared.Infrastructure.Interceptors;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace AsistOff.MES.Multitenancy;

public static class DependencyInjection
{
    public static IServiceCollection AddMultitenancy(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TenantValidationBehavior<,>));
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ICurrentTenantAccessor>(sp => (ICurrentTenantAccessor)sp.GetRequiredService<ITenantContext>());
        services.AddScoped<ITenantRepository, TenantRepository>();

        services.AddDbContext<MultitenancyDbContext>((sp, options) =>
        {
            var connectionString = configuration["postgres:connectionString"];
            var publishDomainEventsInterceptor = sp.GetRequiredService<PublishDomainEventsInterceptor>();
            var auditableEntityInterceptor = sp.GetRequiredService<AuditableEntityInterceptor>();
            options.UseNpgsql(connectionString);
            options.AddInterceptors(publishDomainEventsInterceptor, auditableEntityInterceptor);
        });

        return services;
    }
}