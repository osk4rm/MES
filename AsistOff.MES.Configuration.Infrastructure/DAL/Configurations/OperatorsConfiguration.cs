using AsistOff.MES.Configuration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Configuration.Infrastructure.DAL.Configurations;

public class OperatorsConfiguration : IEntityTypeConfiguration<Operator>
{
    public void Configure(EntityTypeBuilder<Operator> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Identifier)
            .IsRequired()
            .HasMaxLength(50);
        builder.Property(x => x.FirstName)
            .IsRequired()
            .HasMaxLength(100);
        builder.Property(x => x.LastName)
            .IsRequired()
            .HasMaxLength(100);
        builder.Property(x => x.RatePerHour)
            .IsRequired()
            .HasColumnType("decimal(18,5)")
            .HasDefaultValue(0M);
        builder.HasOne(x => x.Department)
            .WithMany(x => x.Employees)
            .HasForeignKey(x => x.DepartmentId)
            .IsRequired();
        
        builder.HasIndex(x => new { x.TenantId, x.Identifier }, "IX_Employees_TenantId_Identifier").IsUnique();
        builder.HasIndex(x => x.DepartmentId, "IX_Employees_DepartmentId");
        builder.HasIndex(x => x.UserId, "IX_Employees_UserId");
        builder.HasIndex(x => x.Identifier, "IX_Employees_Identifier");
        builder.HasIndex(x => x.TenantId, "IX_Employees_TenantId");
    }
}