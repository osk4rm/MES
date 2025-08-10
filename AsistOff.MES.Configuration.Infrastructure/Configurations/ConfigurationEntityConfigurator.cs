using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Configuration.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Configuration.Infrastructure.Configurations;

public class ConfigurationEntityConfigurator : IEntityConfigurator
{
    public void ConfigureEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Warehouse>();
        modelBuilder.Entity<Operator>();
        modelBuilder.Entity<Department>();
        
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConfigurationEntityConfigurator).Assembly);
        
        modelBuilder.Entity<Warehouse>().ToTable("Warehouses", "config");
        modelBuilder.Entity<Operator>().ToTable("Operators", "config");
        modelBuilder.Entity<Department>().ToTable("Departments", "config");
    }
}
