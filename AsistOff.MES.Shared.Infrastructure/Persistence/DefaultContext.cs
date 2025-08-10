using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Infrastructure.Interceptors;
using AsistOff.MES.Users.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Shared.Infrastructure.Persistence;

public class DefaultContext : DbContext
{
    private readonly IEnumerable<IEntityConfigurator> _entityConfigurators;
    private readonly PublishDomainEventsInterceptor _publishDomainEventsInterceptor;
    private readonly AuditableEntityInterceptor _auditableEntityInterceptor;

    public DbSet<User> Users { get; set; }
    public DbSet<Warehouse> Warehouses { get; set; }
    public DbSet<Operator> Operators { get; set; }
    public DbSet<Department> Departments { get; set; }

    public DefaultContext(
        DbContextOptions<DefaultContext> options,
        IEnumerable<IEntityConfigurator> entityConfigurators,
        PublishDomainEventsInterceptor publishDomainEventsInterceptor,
        AuditableEntityInterceptor auditableEntityInterceptor)
        : base(options)
    {
        _entityConfigurators = entityConfigurators;
        _publishDomainEventsInterceptor = publishDomainEventsInterceptor;
        _auditableEntityInterceptor = auditableEntityInterceptor;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var configurator in _entityConfigurators)
        {
            configurator.ConfigureEntities(modelBuilder);
        }
        
        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(_auditableEntityInterceptor);
        optionsBuilder.AddInterceptors(_publishDomainEventsInterceptor);
        base.OnConfiguring(optionsBuilder);
    }
}

