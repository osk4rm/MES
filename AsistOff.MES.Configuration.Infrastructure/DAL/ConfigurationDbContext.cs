using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Shared.Infrastructure.Interceptors;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Configuration.Infrastructure.DAL;

public class ConfigurationDbContext(
    DbContextOptions<ConfigurationDbContext> options,
    PublishDomainEventsInterceptor publishDomainEventsInterceptor) : DefaultContext<ConfigurationDbContext>(options, publishDomainEventsInterceptor)
{
    public DbSet<Warehouse> Warehouses { get; set; }
    public DbSet<Operator> Operators { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("config");
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }
}