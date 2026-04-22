using AsistOff.MES.Configuration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Configuration.Infrastructure.Configurations;

public class ProductPriceConfiguration : IEntityTypeConfiguration<ProductPrice>
{
    public void Configure(EntityTypeBuilder<ProductPrice> builder)
    {
        builder.ToTable("ProductPrices", "config");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.PriceType)
            .IsRequired()
            .HasMaxLength(50);
            
        builder.Property(x => x.Price)
            .IsRequired()
            .HasPrecision(18, 4);
            
        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .HasDefaultValue("PLN");
            
        builder.Property(x => x.MinQuantity)
            .HasPrecision(18, 4)
            .HasDefaultValue(0);
            
        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.HasOne(x => x.Product)
            .WithMany(x => x.ProductPrices)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.MeasureUnit)
            .WithMany()
            .HasForeignKey(x => x.MeasureUnitId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => x.PriceType);
        builder.HasIndex(x => x.ValidFrom);
        builder.HasIndex(x => x.ValidTo);
        builder.HasIndex(x => new { x.ProductId, x.PriceType, x.ValidFrom, x.ValidTo });
    }
}
