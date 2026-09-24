using AsistOff.MES.Production.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Production.Infrastructure.Configurations;

public class OpcUaConnectionConfiguration : IEntityTypeConfiguration<OpcUaConnection>
{
    public void Configure(EntityTypeBuilder<OpcUaConnection> builder)
    {
        builder.ToTable("OpcUaConnections", "production");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EndpointUrl)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(x => x.SecurityPolicy)
            .IsRequired()
            .HasConversion<short>();

        builder.Property(x => x.LastError)
            .HasMaxLength(512);

        // Delete cascades nothing: readings stay attached to tags, which are
        // independent of connections in this slice.

        builder.HasIndex(x => new { x.TenantId, x.MachineId, x.EndpointUrl }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.IsEnabled });
    }
}
