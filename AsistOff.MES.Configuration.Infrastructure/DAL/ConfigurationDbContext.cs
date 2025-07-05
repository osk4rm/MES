using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Multitenancy;
using AsistOff.MES.Multitenancy.Entity;
using AsistOff.MES.Shared.Infrastructure.Interceptors;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Configuration.Infrastructure.DAL;

public class ConfigurationDbContext(
    DbContextOptions<DefaultContext> options,
    PublishDomainEventsInterceptor publishDomainEventsInterceptor) : DefaultContext(options, publishDomainEventsInterceptor)
{
    public DbSet<Warehouse> Warehouses { get; set; }
    public DbSet<Tenant> Tenants { get; set; }
}