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
            .HasConversion<int>();

        builder.Property(x => x.StartTime)
            .IsRequired()
            .HasColumnType("time");

        builder.Property(x => x.EndTime)
            .IsRequired()
            .HasColumnType("time");

        builder.HasOne(x => x.Calendar)
            .WithMany(x => x.Entries)
            .HasForeignKey(x => x.CalendarId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Shift)
            .WithMany()
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.CalendarId);
        builder.HasIndex(x => x.ShiftId);
    }
}
