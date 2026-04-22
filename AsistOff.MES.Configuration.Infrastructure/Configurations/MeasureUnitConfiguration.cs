using AsistOff.MES.Configuration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Configuration.Infrastructure.Configurations;

public class MeasureUnitConfiguration : IEntityTypeConfiguration<MeasureUnit>
{
    public void Configure(EntityTypeBuilder<MeasureUnit> builder)
    {
        builder.ToTable("MeasureUnits", "config");
        
        builder.HasKey(x => x.Id);
            
        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(x => x.Symbol)
            .HasMaxLength(10);
            
        builder.Property(x => x.Description)
            .HasMaxLength(500);
            
        builder.Property(x => x.ConversionFactor)
            .HasPrecision(18, 8);
            
        builder.Property(x => x.Type)
            .IsRequired()
            .HasConversion<short>();

        builder.HasOne(x => x.BaseUnit)
            .WithMany(x => x.DerivedUnits)
            .HasForeignKey(x => x.BaseUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(x => new { x.TenantId, x.Symbol, x.Type }).IsUnique();
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.Type);
    }
}
