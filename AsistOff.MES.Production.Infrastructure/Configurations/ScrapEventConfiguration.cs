using AsistOff.MES.Production.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Production.Infrastructure.Configurations;

public class ScrapEventConfiguration : IEntityTypeConfiguration<ScrapEvent>
{
    public void Configure(EntityTypeBuilder<ScrapEvent> builder)
    {
        builder.ToTable("ScrapEvents", "production");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity)
            .IsRequired()
            .HasColumnType("numeric(14,4)");

        builder.Property(x => x.ReportedAt)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.HasIndex(x => new { x.TenantId, x.MachineId, x.ReportedAt });
        builder.HasIndex(x => x.TenantId);
    }
}
