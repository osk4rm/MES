using AsistOff.MES.Production.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Production.Infrastructure.Configurations;

public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.ToTable("Recipes", "production");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(250);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.SyncId).HasMaxLength(200);

        builder.HasMany(x => x.Versions)
            .WithOne(x => x.Recipe)
            .HasForeignKey(x => x.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.CurrentVersionId);
    }
}
