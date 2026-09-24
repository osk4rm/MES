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

        // Deleting a Work Center must remove its calendar; the machine delete
        // endpoint uses ExecuteDelete, so the cascade has to live in the database.
        builder.HasOne<Machine>()
            .WithMany()
            .HasForeignKey(x => x.MachineId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Entries)
            .WithOne(x => x.WorkCenterCalendar)
            .HasForeignKey(x => x.WorkCenterCalendarId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.MachineId }).IsUnique();
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.MachineId);
    }
}
