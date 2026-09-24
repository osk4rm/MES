using AsistOff.MES.Production.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Production.Infrastructure.Configurations;

public class KanbanCardConfiguration : IEntityTypeConfiguration<KanbanCard>
{
    public void Configure(EntityTypeBuilder<KanbanCard> builder)
    {
        builder.ToTable("KanbanCards", "production");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CardNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Status)
            .HasConversion<short>();

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.HasOne(x => x.Loop)
            .WithMany(x => x.Cards)
            .HasForeignKey(x => x.LoopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.LoopId, x.CardNumber }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.LoopId, x.Status });
        builder.HasIndex(x => x.TenantId);
    }
}
