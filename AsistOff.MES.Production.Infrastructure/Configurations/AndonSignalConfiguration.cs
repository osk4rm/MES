using AsistOff.MES.Production.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Production.Infrastructure.Configurations;

public class AndonSignalConfiguration : IEntityTypeConfiguration<AndonSignal>
{
    public void Configure(EntityTypeBuilder<AndonSignal> builder)
    {
        builder.ToTable("AndonSignals", "production");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Notes)
            .HasMaxLength(2000);

        builder.HasIndex(x => new { x.TenantId, x.MachineId, x.Status, x.RaisedAt });
        builder.HasIndex(x => x.TenantId);
    }
}
