using AsistOff.MES.Production.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Production.Infrastructure.Configurations;

public class RecipeVersionConfiguration : IEntityTypeConfiguration<RecipeVersion>
{
    public void Configure(EntityTypeBuilder<RecipeVersion> builder)
    {
        builder.ToTable("RecipeVersions", "production");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<short>();
        builder.Property(x => x.ChangeNotes).HasMaxLength(2000);

        builder.HasMany(x => x.Operations)
            .WithOne(x => x.RecipeVersion)
            .HasForeignKey(x => x.RecipeVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.RecipeId, x.VersionNumber }).IsUnique();
        builder.HasIndex(x => new { x.RecipeId, x.Status });
        builder.HasIndex(x => x.TenantId);
    }
}
