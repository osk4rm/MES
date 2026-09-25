using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Infrastructure.Configurations;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Production;

/// <summary>
/// Verifies the OEE analytics indexes declared by the EF Core entity
/// configurations: the per-Work Center confirmation window index covering
/// <c>ListForMachineInWindowAsync</c> and the tenant-scoped FK helper used by
/// the summary ideal-cycle-time lookup. The model is built from the
/// configuration classes alone (relationship targets stubbed with keys), so
/// no database is required.
/// </summary>
public class OeeAnalyticsIndexTests
{
    [Fact]
    public void ProductionConfirmationMapping_HasMachineWindowIndex_WithExpectedColumnOrder()
    {
        // Arrange
        var builder = new ModelBuilder();
        builder.Entity<ProductionOrder>(b => b.HasKey(o => o.Id));

        // Act
        new ProductionConfirmationConfiguration().Configure(builder.Entity<ProductionConfirmation>());
        var entityType = builder.FinalizeModel().FindEntityType(typeof(ProductionConfirmation));

        // Assert
        entityType.Should().NotBeNull();
        var index = entityType!.GetIndexes().SingleOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(["TenantId", "MachineId", "ReportedAt"]));

        index.Should().NotBeNull("the OEE snapshot/trend/summary window query filters one Work Center over a ReportedAt range");
        index!.IsUnique.Should().BeFalse();
        index.Properties.Select(p => p.Name).Should().ContainInOrder("TenantId", "MachineId", "ReportedAt");
        index.Properties.First().Name.Should().Be("TenantId", "tenant isolation must lead so the global query filter stays effective");
    }

    [Fact]
    public void ProductionConfirmationMapping_KeepsOrderWindowIndex()
    {
        // Arrange
        var builder = new ModelBuilder();
        builder.Entity<ProductionOrder>(b => b.HasKey(o => o.Id));

        // Act
        new ProductionConfirmationConfiguration().Configure(builder.Entity<ProductionConfirmation>());
        var entityType = builder.FinalizeModel().FindEntityType(typeof(ProductionConfirmation));

        // Assert
        entityType.Should().NotBeNull();
        entityType!.GetIndexes()
            .Where(i => i.Properties.Select(p => p.Name).SequenceEqual(["TenantId", "ProductionOrderId", "ReportedAt"]))
            .Should().HaveCount(1);
    }

    [Fact]
    public void OperationNodeMapping_HasTenantVersionHelperIndex_WithExpectedColumnOrder()
    {
        // Arrange
        var builder = new ModelBuilder();
        builder.Entity<BomItem>(b => b.HasKey(x => x.Id));
        builder.Entity<OperationOutput>(b => b.HasKey(x => x.Id));
        builder.Entity<ResourceRequirement>(b => b.HasKey(x => x.Id));
        builder.Entity<OperationDependency>(b => b.HasKey(x => x.Id));

        // Act
        new OperationNodeConfiguration().Configure(builder.Entity<OperationNode>());
        var entityType = builder.FinalizeModel().FindEntityType(typeof(OperationNode));

        // Assert
        entityType.Should().NotBeNull();
        var index = entityType!.GetIndexes().SingleOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(["TenantId", "RecipeVersionId"]));

        index.Should().NotBeNull("the OEE summary ideal-cycle-time lookup lists operations for one recipe version under the tenant filter");
        index!.IsUnique.Should().BeFalse();
        index.Properties.First().Name.Should().Be("TenantId", "tenant isolation must lead so the global query filter stays effective");
    }

    [Fact]
    public void OperationNodeMapping_KeepsVersionCodeUniqueIndex()
    {
        // Arrange
        var builder = new ModelBuilder();
        builder.Entity<BomItem>(b => b.HasKey(x => x.Id));
        builder.Entity<OperationOutput>(b => b.HasKey(x => x.Id));
        builder.Entity<ResourceRequirement>(b => b.HasKey(x => x.Id));
        builder.Entity<OperationDependency>(b => b.HasKey(x => x.Id));

        // Act
        new OperationNodeConfiguration().Configure(builder.Entity<OperationNode>());
        var entityType = builder.FinalizeModel().FindEntityType(typeof(OperationNode));

        // Assert
        entityType.Should().NotBeNull();
        var unique = entityType!.GetIndexes().SingleOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(["RecipeVersionId", "Code"]));

        unique.Should().NotBeNull();
        unique!.IsUnique.Should().BeTrue();
    }
}
