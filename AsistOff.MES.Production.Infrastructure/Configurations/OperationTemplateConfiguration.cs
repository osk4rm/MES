using AsistOff.MES.Production.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Production.Infrastructure.Configurations;

public class OperationTemplateConfiguration : IEntityTypeConfiguration<OperationTemplate>
{
    public void Configure(EntityTypeBuilder<OperationTemplate> builder)
    {
        builder.ToTable("OperationTemplates", "production");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.OperationType)
            .HasMaxLength(100);

        builder.Property(x => x.SetupTimeMinutes)
            .HasColumnType("numeric(10,4)");

        builder.Property(x => x.RunTimePerUnitSeconds)
            .HasColumnType("numeric(10,4)");

        builder.Property(x => x.RunTimePerBatchMinutes)
            .HasColumnType("numeric(10,4)");

        builder.Property(x => x.TeardownTimeMinutes)
            .HasColumnType("numeric(10,4)");

        builder.Property(x => x.QueueTimeMinutes)
            .HasColumnType("numeric(10,4)");

        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => x.TenantId);
    }
}
