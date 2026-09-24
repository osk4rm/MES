using AsistOff.MES.Production.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Production.Infrastructure.Configurations;

public class ProductionConfirmationConfiguration : IEntityTypeConfiguration<ProductionConfirmation>
{
    public void Configure(EntityTypeBuilder<ProductionConfirmation> builder)
    {
        builder.ToTable("ProductionConfirmations", "production");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.GoodQuantity)
            .IsRequired()
            .HasColumnType("numeric(14,4)");

        builder.Property(x => x.ScrapQuantity)
            .IsRequired()
            .HasColumnType("numeric(14,4)");

        builder.Property(x => x.ReportedAt)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.HasOne(x => x.ProductionOrder)
            .WithMany()
            .HasForeignKey(x => x.ProductionOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.ProductionOrderId, x.ReportedAt });
        builder.HasIndex(x => x.TenantId);
    }
}
