using AsistOff.MES.CustomerOrders.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.CustomerOrders.Infrastructure.Configurations;

public class CustomerOrderLineProductionReleaseConfiguration : IEntityTypeConfiguration<CustomerOrderLineProductionRelease>
{
    public void Configure(EntityTypeBuilder<CustomerOrderLineProductionRelease> builder)
    {
        builder.ToTable("CustomerOrderLineProductionReleases", "customer_orders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Quantity).HasPrecision(18, 6);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasIndex(x => new { x.TenantId, x.CustomerOrderLineId });
        builder.HasIndex(x => new { x.TenantId, x.RecipeId });
        builder.HasIndex(x => x.ProductionOrderId);
    }
}
