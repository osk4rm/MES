using AsistOff.MES.Configuration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Configuration.Infrastructure.Configurations;

public class WorkCenterCalendarConfiguration : IEntityTypeConfiguration<WorkCenterCalendar>
{
    public void Configure(EntityTypeBuilder<WorkCenterCalendar> builder)
    {
        builder.ToTable("WorkCenterCalendars", "config");

        builder.HasKey(x => x.Id);

        // Deleting a Work Center cascades to its calendar (and, transitively, entries).
        builder.HasOne(x => x.Machine)
            .WithMany()
            .HasForeignKey(x => x.MachineId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.MachineId }).IsUnique();
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.MachineId);
    }
}
