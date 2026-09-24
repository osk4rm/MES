using AsistOff.MES.Production.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Production.Infrastructure.Configurations;

public class DowntimeEventConfiguration : IEntityTypeConfiguration<DowntimeEvent>
{
    public void Configure(EntityTypeBuilder<DowntimeEvent> builder)
    {
        builder.ToTable("DowntimeEvents", "production");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.HasIndex(x => new { x.TenantId, x.MachineId, x.StartedAt });
        builder.HasIndex(x => x.TenantId);
    }
}
