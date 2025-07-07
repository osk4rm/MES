using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Shared.Infrastructure.Interceptors;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Configuration.Infrastructure.DAL;

public class ConfigurationDbContext(
    DbContextOptions<DefaultContext> options,
    PublishDomainEventsInterceptor publishDomainEventsInterceptor) : DefaultContext(options, publishDomainEventsInterceptor)
{
    public DbSet<Warehouse> Warehouses { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("config");
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }
}