using AsistOff.MES.Configuration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Configuration.Infrastructure.DAL.Configurations;

public class EmployeesConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
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
    }
}