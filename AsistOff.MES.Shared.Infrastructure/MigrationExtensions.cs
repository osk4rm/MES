using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Shared.Infrastructure;

public static class MigrationExtensions
{
    public static void ApplyPendingMigrations<TContext>(this IServiceProvider serviceProvider)
        where TContext : DbContext
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var pending = context.Database.GetPendingMigrations();
        if (pending.Any())
        {
            context.Database.Migrate();
        }
    }

    public static void ApplyAllPendingMigrations(this IServiceProvider serviceProvider, IEnumerable<Assembly> assemblies)
    {
        var dbContextTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(DbContext).IsAssignableFrom(t)
                && !t.IsAbstract
                && !t.IsGenericType
                && t != typeof(DbContext)
                && t.Name.EndsWith("Context"))
            .ToList();
        
        foreach (var dbContextType in dbContextTypes)
        {
            var method = typeof(MigrationExtensions).GetMethod(nameof(ApplyPendingMigrations))!.MakeGenericMethod(dbContextType);
            method.Invoke(null, new object[] { serviceProvider });
        }
    }
}