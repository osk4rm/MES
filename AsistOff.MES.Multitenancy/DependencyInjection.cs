using AsistOff.MES.Multitenancy.Behaviors;
using AsistOff.MES.Multitenancy.Context;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Multitenancy.Entity;
using AsistOff.MES.Multitenancy.Outbox;
using AsistOff.MES.Multitenancy.Repositories;
using AsistOff.MES.Multitenancy.Seeding;
using AsistOff.MES.Shared.Abstractions.Seeder;
using AsistOff.MES.Shared.Infrastructure.Interceptors;
using AsistOff.MES.Shared.Infrastructure.Outbox;
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
        services.AddScoped<ITenantCreatedEventOutbox, TenantCreatedEventOutboxWriter>();
        services.AddScoped<IOutboxTenantSource, OutboxTenantSource>();

        services.AddDbContext<MultitenancyDbContext>((sp, options) =>
        {
            var connectionString = configuration["postgres:connectionString"];
            var publishDomainEventsInterceptor = sp.GetRequiredService<PublishDomainEventsInterceptor>();
            var auditableEntityInterceptor = sp.GetRequiredService<AuditableEntityInterceptor>();
            options.UseNpgsql(connectionString);
            options.AddInterceptors(publishDomainEventsInterceptor, auditableEntityInterceptor);
        });

        services.Configure<DevTenantSeedOptions>(configuration.GetSection(DevTenantSeedOptions.SectionName));
        services.AddScoped<ISeeder, DevTenantSeeder>();

        return services;
    }
}