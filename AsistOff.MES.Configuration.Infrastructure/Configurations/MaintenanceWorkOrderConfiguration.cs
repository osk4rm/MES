using AsistOff.MES.Configuration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Configuration.Infrastructure.Configurations;

public class MaintenanceWorkOrderConfiguration : IEntityTypeConfiguration<MaintenanceWorkOrder>
{
    public void Configure(EntityTypeBuilder<MaintenanceWorkOrder> builder)
    {
        builder.ToTable("MaintenanceWorkOrders", "config");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.Priority)
            .IsRequired()
            .HasConversion<short>();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<short>();

        builder.Property(x => x.ReportedAt)
            .IsRequired();

        builder.Property(x => x.ResolutionNotes)
            .HasMaxLength(2000);

        builder.HasOne(x => x.Machine)
            .WithMany()
            .HasForeignKey(x => x.MachineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.MachineId);
        builder.HasIndex(x => x.Status);
    }
}
