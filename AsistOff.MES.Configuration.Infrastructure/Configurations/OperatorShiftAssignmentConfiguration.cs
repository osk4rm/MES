using AsistOff.MES.Configuration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Configuration.Infrastructure.Configurations;

public class OperatorShiftAssignmentConfiguration : IEntityTypeConfiguration<OperatorShiftAssignment>
{
    public void Configure(EntityTypeBuilder<OperatorShiftAssignment> builder)
    {
        builder.ToTable("OperatorShiftAssignments", "config");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Date)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.HasOne(x => x.Operator)
            .WithMany()
            .HasForeignKey(x => x.OperatorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Shift)
            .WithMany()
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.OperatorId, x.ShiftId, x.Date }).IsUnique();
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.OperatorId);
        builder.HasIndex(x => x.ShiftId);
        builder.HasIndex(x => x.Date);
    }
}
