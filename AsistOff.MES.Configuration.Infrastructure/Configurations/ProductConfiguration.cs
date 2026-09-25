using AsistOff.MES.Configuration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Configuration.Infrastructure.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", "config");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50);
            
        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);
            
        builder.Property(x => x.Description)
            .HasMaxLength(1000);
            
        builder.Property(x => x.Ean)
            .HasMaxLength(20);
            
        builder.Property(x => x.Barcode)
            .HasMaxLength(50);
            
        builder.Property(x => x.ScanBy)
            .IsRequired()
            .HasConversion<int>();

        builder.HasOne(x => x.ProductGroup)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.ProductGroupId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Ean }).IsUnique().HasFilter("\"Ean\" IS NOT NULL");
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.ProductGroupId);
        builder.HasIndex(x => x.Ean);
        builder.HasIndex(x => x.Barcode);
        builder.HasIndex(x => x.ScanBy);
    }
}
