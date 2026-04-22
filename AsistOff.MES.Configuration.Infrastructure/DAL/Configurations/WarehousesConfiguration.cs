using AsistOff.MES.Configuration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AsistOff.MES.Configuration.Infrastructure.DAL.Configurations;

public class WarehousesConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired();
        
        builder.HasIndex(x => x.TenantId, "IX_Warehouses_TenantId");
        builder.HasIndex(x => x.Name, "IX_Warehouses_Name");
    }
}