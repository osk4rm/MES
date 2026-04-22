using AsistOff.MES.Configuration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Configuration.Infrastructure.Configurations;

public class ProductMeasureUnitConfiguration : IEntityTypeConfiguration<ProductMeasureUnit>
{
    public void Configure(EntityTypeBuilder<ProductMeasureUnit> builder)
    {
        builder.ToTable("ProductMeasureUnits", "config");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.ConversionFactor)
            .IsRequired()
            .HasPrecision(18, 8)
            .HasDefaultValue(1);
            
        builder.Property(x => x.Description)
            .HasMaxLength(500);

        // Relationships
        builder.HasOne(x => x.Product)
            .WithMany(x => x.ProductMeasureUnits)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.MeasureUnit)
            .WithMany(x => x.ProductMeasureUnits)
            .HasForeignKey(x => x.MeasureUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(x => new { x.TenantId, x.ProductId, x.MeasureUnitId }).IsUnique();
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => x.MeasureUnitId);
        builder.HasIndex(x => x.IsDefault);
    }
}
