using AsistOff.MES.Production.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Production.Infrastructure.Configurations;

public class SpcMeasurementConfiguration : IEntityTypeConfiguration<SpcMeasurement>
{
    public void Configure(EntityTypeBuilder<SpcMeasurement> builder)
    {
        builder.ToTable("SpcMeasurements", "production");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Value)
            .IsRequired()
            .HasColumnType("numeric(18,6)");

        builder.Property(x => x.MeasuredAt)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.HasOne(x => x.Characteristic)
            .WithMany()
            .HasForeignKey(x => x.CharacteristicId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.CharacteristicId, x.MeasuredAt });
        builder.HasIndex(x => x.TenantId);
    }
}
