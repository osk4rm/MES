using AsistOff.MES.Production.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Production.Infrastructure.Configurations;

public class KanbanLoopConfiguration : IEntityTypeConfiguration<KanbanLoop>
{
    public void Configure(EntityTypeBuilder<KanbanLoop> builder)
    {
        builder.ToTable("KanbanLoops", "production");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.CardQuantity)
            .HasColumnType("numeric(18,4)");

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.ProductId });
    }
}
