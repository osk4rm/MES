using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Production.Domain.Entities;

/// <summary>
/// Header of a production order: an instruction to manufacture a planned
/// quantity of a product from a released recipe version.
/// </summary>
public class ProductionOrder : IEntity, ISaasy, IAuditable, ISyncable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? SyncId { get; set; }
    public required string Code { get; set; }
    public Guid ProductId { get; set; }
    public Guid RecipeId { get; set; }
    public Guid RecipeVersionId { get; set; }
    public decimal PlannedQuantity { get; set; }
    public Guid? MeasureUnitId { get; set; }
    public int Priority { get; set; }
    public DateTime? DueDate { get; set; }
    public ProductionOrderStatus Status { get; set; } = ProductionOrderStatus.Planned;
    public DateTime? ReleasedAt { get; set; }
    public Guid? ReleasedByUserId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }
}
