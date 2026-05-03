using AsistOff.MES.CustomerOrders.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.CustomerOrders.Domain.Entities;

public class CustomerOrderLineProductionRelease : IEntity, ISaasy, IAuditable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CustomerOrderLineId { get; set; }
    public Guid RecipeId { get; set; }
    public Guid RecipeVersionId { get; set; }
    public decimal Quantity { get; set; }
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedDueDate { get; set; }
    public Guid? ProductionOrderId { get; set; }
    public ProductionReleaseStatus Status { get; set; } = ProductionReleaseStatus.Planned;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual CustomerOrderLine CustomerOrderLine { get; set; } = null!;
}
