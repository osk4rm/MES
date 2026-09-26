using AsistOff.MES.Production.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Production.Infrastructure.Configurations;

public class ShiftHandoverConfiguration : IEntityTypeConfiguration<ShiftHandover>
{
    public void Configure(EntityTypeBuilder<ShiftHandover> builder)
    {
        builder.ToTable("ShiftHandovers", "production");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Notes)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.From)
            .IsRequired();

        builder.Property(x => x.To)
            .IsRequired();

        // One logbook entry per shift boundary: prevents double-logging the
        // same (MachineId, From) boundary within a tenant.
        builder.HasIndex(x => new { x.TenantId, x.MachineId, x.From })
            .IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.MachineId });
        builder.HasIndex(x => x.TenantId);
    }
}
