using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.DAL;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Production.Infrastructure.Configurations;

public class ProductionEntityConfigurator : IEntityConfigurator
{
    public void ConfigureEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Recipe>();
        modelBuilder.Entity<RecipeVersion>();
        modelBuilder.Entity<OperationNode>();
        modelBuilder.Entity<OperationDependency>();
        modelBuilder.Entity<BomItem>();
        modelBuilder.Entity<OperationOutput>();
        modelBuilder.Entity<ResourceRequirement>();
        modelBuilder.Entity<OperationTemplate>();
        modelBuilder.Entity<ProductionOrder>();
        modelBuilder.Entity<ProductionConfirmation>();
        modelBuilder.Entity<SpcCharacteristic>();
        modelBuilder.Entity<DowntimeEvent>();
        modelBuilder.Entity<Lot>();
        modelBuilder.Entity<LotGenealogyEdge>();
        modelBuilder.Entity<ScrapEvent>();
        modelBuilder.Entity<MachineTelemetryTag>();
        modelBuilder.Entity<TelemetryReading>();
        modelBuilder.Entity<OpcUaConnection>();
        modelBuilder.Entity<KanbanLoop>();
        modelBuilder.Entity<KanbanCard>();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProductionEntityConfigurator).Assembly);
    }
}
