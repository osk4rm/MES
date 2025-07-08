using AsistOff.MES.Users.Core.Repositories;
using AsistOff.MES.Users.Infrastructure.DAL;
using AsistOff.MES.Shared.Infrastructure.Interceptors;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using AsistOff.MES.Users.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Users.Infrastructure;

public static class Extensions
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IUsersRepository, UsersRepository>();
        services.AddDbContext<UsersDbContext>((sp, options) =>
        {
            var connectionString = configuration["postgres:connectionString"];
            var publishDomainEventsInterceptor = sp.GetRequiredService<PublishDomainEventsInterceptor>();
            var auditableEntityInterceptor = sp.GetRequiredService<AuditableEntityInterceptor>();
            options.UseNpgsql(connectionString);
            options.AddInterceptors(publishDomainEventsInterceptor, auditableEntityInterceptor);
        });
    }
}