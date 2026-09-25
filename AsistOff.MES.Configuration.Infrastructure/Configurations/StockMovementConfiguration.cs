using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Production.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Configuration.Infrastructure.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements", "config");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.MovementType)
            .IsRequired()
            .HasMaxLength(2);

        builder.Property(x => x.Quantity)
            .IsRequired()
            .HasColumnType("numeric(14,4)");

        builder.Property(x => x.ReportedAt)
            .IsRequired();

        // Loose Guid FK to the confirmation in the production schema. The
        // database cascade keeps the delete plus re-create correction model
        // consistent: deleting a confirmation removes its ledger lines.
        builder.HasOne<ProductionConfirmation>()
            .WithMany()
            .HasForeignKey(x => x.ProductionConfirmationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.ProductionConfirmationId });
        builder.HasIndex(x => new { x.TenantId, x.ProductionOrderId });
        builder.HasIndex(x => x.TenantId);
    }
}
