using AsistOff.MES.Production.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Production.Infrastructure.Configurations;

public class ScheduledOperationConfiguration : IEntityTypeConfiguration<ScheduledOperation>
{
    public void Configure(EntityTypeBuilder<ScheduledOperation> builder)
    {
        builder.ToTable("ScheduledOperations", "production");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Notes).HasMaxLength(2000);

        // One override per operation of an order within a tenant: recomputation
        // overlays at most one manual row per (order, operation).
        builder.HasIndex(x => new { x.TenantId, x.ProductionOrderId, x.OperationNodeId })
            .IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ProductionOrderId });
        builder.HasIndex(x => new { x.TenantId, x.MachineId });
        builder.HasIndex(x => x.TenantId);
    }
}
