using AsistOff.MES.Configuration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Configuration.Infrastructure.Configurations;

public class OperatorSkillQualificationConfiguration : IEntityTypeConfiguration<OperatorSkillQualification>
{
    public void Configure(EntityTypeBuilder<OperatorSkillQualification> builder)
    {
        builder.ToTable("OperatorSkillQualifications", "config");

        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Operator)
            .WithMany()
            .HasForeignKey(x => x.OperatorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Skill)
            .WithMany()
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.OperatorId, x.SkillId }).IsUnique();
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.OperatorId);
        builder.HasIndex(x => x.SkillId);
    }
}
