using AsistOff.MES.Multitenancy.Entity;
using AsistOff.MES.Shared.Infrastructure.Interceptors;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using AsistOff.MES.Users.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Users.Infrastructure.DAL;

public class UsersDbContext(
    DbContextOptions<DefaultContext> options,
    PublishDomainEventsInterceptor publishDomainEventsInterceptor,
    AuditableEntityInterceptor auditableEntityInterceptor)
    : DefaultContext(options, publishDomainEventsInterceptor)
{
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("users");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UsersDbContext).Assembly);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(auditableEntityInterceptor);
        base.OnConfiguring(optionsBuilder);
    }
}