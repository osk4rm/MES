using System.Reflection;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Shared.Infrastructure.Auth;
using AsistOff.MES.Shared.Infrastructure.Behaviors;
using AsistOff.MES.Shared.Infrastructure.Events;
using AsistOff.MES.Shared.Infrastructure.Interceptors;
using AsistOff.MES.Shared.Infrastructure.Messaging;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using AsistOff.MES.Shared.Infrastructure.Providers;
using AsistOff.MES.Shared.Infrastructure.Validation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AsistOff.MES.Shared.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services,
            IConfiguration configuration, IList<Assembly> assemblies, IHostEnvironment? hostEnvironment = null)
        {
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
            services.AddSingleton<IGuidProvider, GuidProvider>();
            services.AddScoped<ICurrentUserAccessor, HttpCurrentUserAccessor>();
            services.AddAuth(hostEnvironment);
            services.AddMessaging();
            services.AddValidation(assemblies);
            services.AddPersistence(configuration);
            services.AddEvents(assemblies);

            services.AddDbContext<DefaultContext>(options =>
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection") 
                    ?? configuration["postgres:connectionString"];
                options.UseNpgsql(connectionString);
            });

            return services;
        }

        private static IServiceCollection AddPersistence(this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddScoped<PublishDomainEventsInterceptor>();
            services.AddSingleton<AuditableEntityInterceptor>();
            services.AddScoped<SaasyEntityInterceptor>();

            return services;
        }

        
    }
}
