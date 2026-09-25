using AsistOff.MES.Production.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Production.Infrastructure.Configurations;

public class ProductionOrderConfiguration : IEntityTypeConfiguration<ProductionOrder>
{
    public void Configure(EntityTypeBuilder<ProductionOrder> builder)
    {
        builder.ToTable("ProductionOrders", "production");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.SyncId).HasMaxLength(200);
        builder.Property(x => x.Status).HasConversion<short>();
        builder.Property(x => x.PlannedQuantity).HasColumnType("numeric(14,4)");

        // Optimistic concurrency via the PostgreSQL xmin system column
        // (issue #263). Npgsql maps uint row-version properties onto xmin,
        // which changes on every committed write and never emits DDL for it.
        builder.Property(x => x.Xmin).HasColumnName("xmin").IsRowVersion();

        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.Status });
        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => x.RecipeId);
        builder.HasIndex(x => x.RecipeVersionId);
    }
}
