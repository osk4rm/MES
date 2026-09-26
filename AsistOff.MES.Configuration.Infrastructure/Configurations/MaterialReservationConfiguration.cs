using AsistOff.MES.Configuration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Configuration.Infrastructure.Configurations;

public class MaterialReservationConfiguration : IEntityTypeConfiguration<MaterialReservation>
{
    public void Configure(EntityTypeBuilder<MaterialReservation> builder)
    {
        builder.ToTable("MaterialReservations", "config");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.QuantityReserved)
            .IsRequired()
            .HasColumnType("numeric(14,4)");

        builder.Property(x => x.QuantityRelieved)
            .IsRequired()
            .HasColumnType("numeric(14,4)");

        // Release idempotency backstop (issue #291): re-releasing an order
        // skips already-reserved pairs in code; the constraint rejects any
        // residual double-insert race for warehouse-bound rows. Null
        // warehouse rows (the unassigned bucket) are not deduplicated by the
        // index — PostgreSQL treats NULLs as distinct — so the code-level
        // skip remains the primary guard for those.
        builder.HasIndex(x => new { x.TenantId, x.ProductionOrderId, x.ProductId, x.WarehouseId })
            .IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ProductionOrderId });
        builder.HasIndex(x => x.TenantId);
    }
}
