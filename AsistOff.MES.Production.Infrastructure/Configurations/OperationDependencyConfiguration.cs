using AsistOff.MES.Production.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Production.Infrastructure.Configurations;

public class OperationDependencyConfiguration : IEntityTypeConfiguration<OperationDependency>
{
    public void Configure(EntityTypeBuilder<OperationDependency> builder)
    {
        builder.ToTable("OperationDependencies", "production");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DependencyType).HasConversion<short>();
        builder.Property(x => x.LagMinutes).HasPrecision(18, 4);

        // The "successor" side is configured in OperationNodeConfiguration.
        // Here we wire the predecessor relationship (no inverse navigation to
        // avoid coupling the entity with a second collection).
        builder.HasOne(x => x.PredecessorOperationNode)
            .WithMany()
            .HasForeignKey(x => x.PredecessorOperationNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.OperationNodeId, x.PredecessorOperationNodeId }).IsUnique();
        builder.HasIndex(x => x.RecipeVersionId);
        builder.HasIndex(x => x.TenantId);
    }
}
