using AsistOff.MES.CustomerOrders.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.CustomerOrders.Infrastructure.Configurations;

public class CustomerOrderLineConfiguration : IEntityTypeConfiguration<CustomerOrderLine>
{
    public void Configure(EntityTypeBuilder<CustomerOrderLine> builder)
    {
        builder.ToTable("CustomerOrderLines", "customer_orders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SyncId).HasMaxLength(200);
        builder.Property(x => x.ExternalLineId).HasMaxLength(200);
        builder.Property(x => x.ProductCodeSnapshot).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ProductNameSnapshot).IsRequired().HasMaxLength(250);
        builder.Property(x => x.MeasureUnitCodeSnapshot).HasMaxLength(50);
        builder.Property(x => x.OrderedQuantity).HasPrecision(18, 6);
        builder.Property(x => x.ReleasedQuantity).HasPrecision(18, 6);
        builder.Property(x => x.UnitNetPrice).HasPrecision(18, 4);
        builder.Property(x => x.LineNetAmount).HasPrecision(18, 4);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Ignore(x => x.RemainingQuantity);
        builder.HasMany(x => x.ProductionReleases)
            .WithOne(x => x.CustomerOrderLine)
            .HasForeignKey(x => x.CustomerOrderLineId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.TenantId, x.CustomerOrderId });
        builder.HasIndex(x => new { x.TenantId, x.ProductId });
        builder.HasIndex(x => new { x.TenantId, x.Status });
    }
}
