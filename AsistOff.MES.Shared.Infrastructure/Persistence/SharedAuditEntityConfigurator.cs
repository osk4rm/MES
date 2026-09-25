using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Shared.Infrastructure.Persistence;

/// <summary>
/// Registers shared audit tables (currently <c>AuditEvents</c>) on the
/// <see cref="DefaultContext"/> model. Owned by slice 1 (#245) so slice 2
/// (#246) consumes the schema with no new migration.
/// </summary>
public class SharedAuditEntityConfigurator : IEntityConfigurator
{
    public void ConfigureEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
    }
}
