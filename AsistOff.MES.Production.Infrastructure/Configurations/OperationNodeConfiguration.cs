using AsistOff.MES.Production.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Production.Infrastructure.Configurations;

public class OperationNodeConfiguration : IEntityTypeConfiguration<OperationNode>
{
    public void Configure(EntityTypeBuilder<OperationNode> builder)
    {
        builder.ToTable("OperationNodes", "production");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(250);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.OperationType).HasMaxLength(100);
        builder.Property(x => x.RunTimeMode).HasConversion<short>();

        builder.Property(x => x.SetupTimeMinutes).HasPrecision(18, 4);
        builder.Property(x => x.RunTimePerUnitSeconds).HasPrecision(18, 4);
        builder.Property(x => x.RunTimePerBatchMinutes).HasPrecision(18, 4);
        builder.Property(x => x.TeardownTimeMinutes).HasPrecision(18, 4);
        builder.Property(x => x.QueueTimeMinutes).HasPrecision(18, 4);
        builder.Property(x => x.ExpectedQuantity).HasPrecision(18, 4);

        builder.HasMany(x => x.BomItems)
            .WithOne(x => x.OperationNode)
            .HasForeignKey(x => x.OperationNodeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Outputs)
            .WithOne(x => x.OperationNode)
            .HasForeignKey(x => x.OperationNodeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.ResourceRequirements)
            .WithOne(x => x.OperationNode)
            .HasForeignKey(x => x.OperationNodeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Dependencies where this node is the successor. The "predecessor" side
        // is configured by OperationDependencyConfiguration.
        builder.HasMany(x => x.Dependencies)
            .WithOne(x => x.OperationNode)
            .HasForeignKey(x => x.OperationNodeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.RecipeVersionId, x.Code }).IsUnique();
        // OEE summary read path: ListForVersionAsync filters one recipe
        // version under the tenant global query filter when resolving the
        // ideal cycle time from confirmed orders. Tenant-leading so isolation
        // holds with no IgnoreQueryFilters bypass.
        builder.HasIndex(x => new { x.TenantId, x.RecipeVersionId });
        builder.HasIndex(x => x.TenantId);
    }
}
