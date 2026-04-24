using AsistOff.MES.Production.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Production.Infrastructure.Configurations;

public class OperationOutputConfiguration : IEntityTypeConfiguration<OperationOutput>
{
    public void Configure(EntityTypeBuilder<OperationOutput> builder)
    {
        builder.ToTable("OperationOutputs", "production");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.QuantityType).HasConversion<short>();
        builder.Property(x => x.OutputType).HasConversion<short>();
        builder.Property(x => x.Notes).HasMaxLength(2000);

        builder.HasIndex(x => x.OperationNodeId);
        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => x.TenantId);
    }
}
