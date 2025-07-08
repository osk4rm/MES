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

    public DbSet<Tenant> Tenants { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("multitenancy");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MultitenancyDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => t.Name).IsUnique();
            entity.Property(t => t.Name).IsRequired();
            entity.Property(t => t.CreatedAt).IsRequired();
            entity.Property(t => t.UpdatedAt).IsRequired();
            entity.Property(t => t.IsActive).IsRequired();
            entity.Property(t => t.Settings).HasColumnType("jsonb");
        });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(_auditableEntityInterceptor);
        base.OnConfiguring(optionsBuilder);
    }
}
