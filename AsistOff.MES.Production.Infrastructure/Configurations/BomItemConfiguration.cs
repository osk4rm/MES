using AsistOff.MES.Production.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Production.Infrastructure.Configurations;

public class BomItemConfiguration : IEntityTypeConfiguration<BomItem>
{
    public void Configure(EntityTypeBuilder<BomItem> builder)
    {
        builder.ToTable("BomItems", "production");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.ScrapPercentage).HasPrecision(9, 4);
        builder.Property(x => x.QuantityType).HasConversion<short>();
        builder.Property(x => x.ConsumptionTiming).HasConversion<short>();
        builder.Property(x => x.Notes).HasMaxLength(2000);

        builder.HasIndex(x => x.OperationNodeId);
        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => x.TenantId);
    }
}
