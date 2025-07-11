using AsistOff.MES.Multitenancy.Entity;
using AsistOff.MES.Shared.Infrastructure.Interceptors;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Multitenancy.Context;

public class MultitenancyDbContext : DefaultContext<MultitenancyDbContext>
{
    public MultitenancyDbContext(
        DbContextOptions<MultitenancyDbContext> options,
        PublishDomainEventsInterceptor publishDomainEventsInterceptor,
        AuditableEntityInterceptor auditableEntityInterceptor)
        : base(options, publishDomainEventsInterceptor)
    {
        _auditableEntityInterceptor = auditableEntityInterceptor;
    }

    private readonly AuditableEntityInterceptor _auditableEntityInterceptor;

    public DbSet<Tenant> Tenants { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("multitenancy");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MultitenancyDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(_auditableEntityInterceptor);
        base.OnConfiguring(optionsBuilder);
    }
}
