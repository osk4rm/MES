using AsistOff.MES.Production.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Production.Infrastructure.Configurations;

public class TelemetryReadingConfiguration : IEntityTypeConfiguration<TelemetryReading>
{
    public void Configure(EntityTypeBuilder<TelemetryReading> builder)
    {
        builder.ToTable("TelemetryReadings", "production");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StringValue)
            .HasMaxLength(256);

        builder.Property(x => x.Quality)
            .IsRequired()
            .HasConversion<short>();

        builder.HasOne(r => r.Tag)
            .WithMany()
            .HasForeignKey(r => r.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.TagId, x.ReadAt });
        builder.HasIndex(x => new { x.TenantId, x.MachineId, x.ReadAt });
    }
}
