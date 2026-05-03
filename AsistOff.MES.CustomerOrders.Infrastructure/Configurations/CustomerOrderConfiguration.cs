using AsistOff.MES.CustomerOrders.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.CustomerOrders.Infrastructure.Configurations;

public class CustomerOrderConfiguration : IEntityTypeConfiguration<CustomerOrder>
{
    public void Configure(EntityTypeBuilder<CustomerOrder> builder)
    {
        builder.ToTable("CustomerOrders", "customer_orders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OrderNumber).IsRequired().HasMaxLength(100);
        builder.Property(x => x.SyncId).HasMaxLength(200);
        builder.Property(x => x.ExternalSystem).HasMaxLength(100);
        builder.Property(x => x.ExternalOrderId).HasMaxLength(200);
        builder.Property(x => x.CustomerCodeSnapshot).IsRequired().HasMaxLength(50);
        builder.Property(x => x.CustomerNameSnapshot).IsRequired().HasMaxLength(250);
        builder.Property(x => x.CustomerTaxIdSnapshot).HasMaxLength(50);
        builder.Property(x => x.CustomerAddressSnapshot).HasMaxLength(1000);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.TotalNetAmount).HasPrecision(18, 4);
        builder.Property(x => x.TotalGrossAmount).HasPrecision(18, 4);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasOne(x => x.Customer)
            .WithMany(x => x.Orders)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines)
            .WithOne(x => x.CustomerOrder)
            .HasForeignKey(x => x.CustomerOrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.TenantId, x.OrderNumber }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ExternalSystem, x.ExternalOrderId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.CustomerId });
        builder.HasIndex(x => new { x.TenantId, x.Status });
        builder.HasIndex(x => x.RequestedDeliveryDate);
    }
}
