using AsistOff.MES.Shared.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Shared.Infrastructure.Persistence.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages", "shared");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.Payload)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(x => x.OccurredOnUtc)
            .IsRequired();

        builder.Property(x => x.Dispatched)
            .IsRequired();

        builder.Property(x => x.RetryCount)
            .IsRequired();

        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.Dispatched, x.OccurredOnUtc });
    }
}
