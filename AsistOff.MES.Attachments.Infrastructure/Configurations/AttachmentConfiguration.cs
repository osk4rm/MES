using AsistOff.MES.Attachments.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.DAL;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Attachments.Infrastructure.Configurations;

public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachments", "files");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OwnerType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.FileName).IsRequired().HasMaxLength(500);
        builder.Property(x => x.ContentType).IsRequired().HasMaxLength(200);
        builder.Property(x => x.StorageKey).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.Description).HasMaxLength(2000);

        builder.HasIndex(x => new { x.OwnerType, x.OwnerId });
        builder.HasIndex(x => x.TenantId);
    }
}

public class AttachmentsEntityConfigurator : IEntityConfigurator
{
    public void ConfigureEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Attachment>();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AttachmentsEntityConfigurator).Assembly);
    }
}
