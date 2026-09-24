using AsistOff.MES.Production.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Production.Infrastructure.Configurations;

public class LotGenealogyEdgeConfiguration : IEntityTypeConfiguration<LotGenealogyEdge>
{
    public void Configure(EntityTypeBuilder<LotGenealogyEdge> builder)
    {
        builder.ToTable("LotGenealogyEdges", "production");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ConsumedQuantity)
            .IsRequired()
            .HasColumnType("numeric(14,4)");

        builder.Property(x => x.OccurredAt)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.HasOne(x => x.ConsumedLot)
            .WithMany()
            .HasForeignKey(x => x.ConsumedLotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProducedLot)
            .WithMany()
            .HasForeignKey(x => x.ProducedLotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductionOrder)
            .WithMany()
            .HasForeignKey(x => x.ProductionOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProductionConfirmation)
            .WithMany()
            .HasForeignKey(x => x.ProductionConfirmationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.TenantId, x.ProducedLotId });
        builder.HasIndex(x => new { x.TenantId, x.ConsumedLotId });
        builder.HasIndex(x => new { x.TenantId, x.ProductionOrderId, x.OccurredAt });
        builder.HasIndex(x => x.TenantId);
    }
}
