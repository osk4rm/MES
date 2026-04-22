using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Shared.Infrastructure.Persistence;

public static class Extensions
{
    public static IServiceCollection AddPostgres<T>(this IServiceCollection services,
        IConfiguration configuration) where T : DbContext
    {
        var options = configuration.GetOptions<PostgresOptions>("postgres");
        services.AddDbContext<T>(x => x.UseNpgsql(options.ConnectionString));

        return services;
    }
}
