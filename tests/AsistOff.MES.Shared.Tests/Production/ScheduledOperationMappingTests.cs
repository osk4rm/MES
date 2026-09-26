using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Infrastructure.Configurations;
using AsistOff.MES.Shared.Abstractions.DAL;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Production;

/// <summary>
/// Verifies the persisted <see cref="ScheduledOperation"/> override mapping
/// (issue #304, Gantt slice 1/3): tenant-scoped contract, table mapping and
/// the tenant-scoped unique constraint the Gantt read-model relies on. The
/// model is built from the configuration class alone, so no database is
/// required.
/// </summary>
public class ScheduledOperationMappingTests
{
    [Fact]
    public void ScheduledOperation_IsTenantScopedAuditableEntity()
    {
        typeof(ISaasy).IsAssignableFrom(typeof(ScheduledOperation)).Should().BeTrue(
            "the global query filter only isolates ISaasy entities");

        typeof(IAuditable).IsAssignableFrom(typeof(ScheduledOperation)).Should().BeTrue();

        typeof(IEntity).IsAssignableFrom(typeof(ScheduledOperation)).Should().BeTrue();
    }

    [Fact]
    public void ScheduledOperationMapping_UsesProductionSchemaTable()
    {
        var entityType = BuildEntityType();

        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("ScheduledOperations");
        entityType.GetSchema().Should().Be("production");
    }

    [Fact]
    public void ScheduledOperationMapping_HasTenantOrderOperationUniqueIndex()
    {
        var entityType = BuildEntityType();

        entityType.Should().NotBeNull();
        var unique = entityType!.GetIndexes().SingleOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(["TenantId", "ProductionOrderId", "OperationNodeId"]));

        unique.Should().NotBeNull(
            "at most one manual override may exist per (order, operation) within a tenant");
        unique!.IsUnique.Should().BeTrue();
        unique.Properties.First().Name.Should().Be("TenantId",
            "tenant isolation must lead so the global query filter stays effective");
    }

    [Fact]
    public void ScheduledOperationMapping_KeepsTenantOrderHelperIndex()
    {
        var entityType = BuildEntityType();

        entityType.Should().NotBeNull();
        entityType!.GetIndexes()
            .Where(i => !i.IsUnique && i.Properties.Select(p => p.Name)
                .SequenceEqual(["TenantId", "ProductionOrderId"]))
            .Should().HaveCount(1,
                "ListForOrdersAsync filters overrides for one order set under the tenant filter");
    }

    private static Microsoft.EntityFrameworkCore.Metadata.IEntityType? BuildEntityType()
    {
        var builder = new ModelBuilder();
        new ScheduledOperationConfiguration().Configure(builder.Entity<ScheduledOperation>());
        return builder.FinalizeModel().FindEntityType(typeof(ScheduledOperation));
    }
}
