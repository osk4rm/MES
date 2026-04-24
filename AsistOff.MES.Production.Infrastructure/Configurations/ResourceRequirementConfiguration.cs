using AsistOff.MES.Production.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Production.Infrastructure.Configurations;

public class ResourceRequirementConfiguration : IEntityTypeConfiguration<ResourceRequirement>
{
    public void Configure(EntityTypeBuilder<ResourceRequirement> builder)
    {
        builder.ToTable("ResourceRequirements", "production");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RequiredCapability).HasMaxLength(250);
        builder.Property(x => x.RequiredRole).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(2000);

        builder.HasIndex(x => x.OperationNodeId);
        builder.HasIndex(x => x.TenantId);
    }
}
