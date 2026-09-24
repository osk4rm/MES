using AsistOff.MES.Production.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Production.Infrastructure.Configurations;

public class SpcCharacteristicConfiguration : IEntityTypeConfiguration<SpcCharacteristic>
{
    public void Configure(EntityTypeBuilder<SpcCharacteristic> builder)
    {
        builder.ToTable("SpcCharacteristics", "production");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.ChartType)
            .IsRequired()
            .HasConversion<short>();

        builder.Property(x => x.NominalValue)
            .HasColumnType("numeric(18,6)");

        builder.Property(x => x.LowerSpecLimit)
            .HasColumnType("numeric(18,6)");

        builder.Property(x => x.UpperSpecLimit)
            .HasColumnType("numeric(18,6)");

        builder.Property(x => x.LowerControlLimit)
            .HasColumnType("numeric(18,6)");

        builder.Property(x => x.UpperControlLimit)
            .HasColumnType("numeric(18,6)");

        builder.Property(x => x.Unit)
            .HasMaxLength(20);

        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.IsActive });
    }
}
