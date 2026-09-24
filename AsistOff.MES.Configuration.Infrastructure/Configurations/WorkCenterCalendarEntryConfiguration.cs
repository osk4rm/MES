using AsistOff.MES.Configuration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Configuration.Infrastructure.Configurations;

public class WorkCenterCalendarEntryConfiguration : IEntityTypeConfiguration<WorkCenterCalendarEntry>
{
    public void Configure(EntityTypeBuilder<WorkCenterCalendarEntry> builder)
    {
        builder.ToTable("WorkCenterCalendarEntries", "config");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DayOfWeek)
            .IsRequired()
            .HasConversion<short>();

        builder.Property(x => x.StartTime)
            .IsRequired();

        builder.Property(x => x.EndTime)
            .IsRequired();

        // Removing a shift must not delete calendar history - the entry simply
        // loses its shift link.
        builder.HasOne<Shift>()
            .WithMany()
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.WorkCenterCalendarId, x.DayOfWeek });
        builder.HasIndex(x => x.ShiftId);
    }
}
