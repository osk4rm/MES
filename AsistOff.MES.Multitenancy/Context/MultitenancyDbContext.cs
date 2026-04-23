using AsistOff.MES.Multitenancy.Entity;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Multitenancy.Context;

public class MultitenancyDbContext : DbContext
{
    public MultitenancyDbContext(DbContextOptions<MultitenancyDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("multitenancy");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MultitenancyDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
