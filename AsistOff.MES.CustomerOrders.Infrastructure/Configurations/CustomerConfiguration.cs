using AsistOff.MES.CustomerOrders.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.CustomerOrders.Infrastructure.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers", "customer_orders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(250);
        builder.Property(x => x.TaxId).HasMaxLength(50);
        builder.Property(x => x.Email).HasMaxLength(250);
        builder.Property(x => x.Phone).HasMaxLength(50);
        builder.Property(x => x.AddressLine1).HasMaxLength(250);
        builder.Property(x => x.AddressLine2).HasMaxLength(250);
        builder.Property(x => x.PostalCode).HasMaxLength(30);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.Country).HasMaxLength(100);
        builder.Property(x => x.SyncId).HasMaxLength(200);
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.SyncId);
    }
}
