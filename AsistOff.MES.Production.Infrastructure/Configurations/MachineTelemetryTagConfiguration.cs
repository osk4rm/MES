using AsistOff.MES.Production.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Production.Infrastructure.Configurations;

public class MachineTelemetryTagConfiguration : IEntityTypeConfiguration<MachineTelemetryTag>
{
    public void Configure(EntityTypeBuilder<MachineTelemetryTag> builder)
    {
        builder.ToTable("MachineTelemetryTags", "production");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NodeId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.DataType)
            .IsRequired()
            .HasConversion<short>();

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.HasIndex(x => new { x.TenantId, x.MachineId, x.NodeId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.MachineId, x.IsEnabled });
    }
}
