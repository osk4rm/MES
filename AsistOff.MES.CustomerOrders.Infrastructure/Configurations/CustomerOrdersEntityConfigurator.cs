using AsistOff.MES.CustomerOrders.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.DAL;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.CustomerOrders.Infrastructure.Configurations;

public class CustomerOrdersEntityConfigurator : IEntityConfigurator
{
    public void ConfigureEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>();
        modelBuilder.Entity<CustomerOrder>();
        modelBuilder.Entity<CustomerOrderLine>();
        modelBuilder.Entity<CustomerOrderLineProductionRelease>();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomerOrdersEntityConfigurator).Assembly);
    }
}
