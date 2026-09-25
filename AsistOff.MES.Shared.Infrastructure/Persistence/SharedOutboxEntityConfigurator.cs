using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Shared.Infrastructure.Persistence;

/// <summary>
/// Registers shared outbox tables (currently <c>OutboxMessages</c>) on the
/// <see cref="DefaultContext"/> model. Owned by slice 1 (#258) so the slice-2
/// relay consumes the schema with no new migration.
/// </summary>
public class SharedOutboxEntityConfigurator : IEntityConfigurator
{
    public void ConfigureEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}
