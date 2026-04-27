using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Production.Domain.Entities;

/// <summary>
/// An input material consumed by an operation. Attached to
/// <see cref="OperationNode"/>, not to the recipe header, giving per-step
/// control over consumption semantics (operation-level BOM).
/// </summary>
public class BomItem : IEntity, ISaasy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid OperationNodeId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? MeasureUnitId { get; set; }
    public decimal Quantity { get; set; }
    public BomQuantityType QuantityType { get; set; } = BomQuantityType.PerUnit;
    public decimal? ScrapPercentage { get; set; }
    public bool IsOptional { get; set; }
    public Guid? PreferredWarehouseId { get; set; }
    public ConsumptionTiming ConsumptionTiming { get; set; } = ConsumptionTiming.AtStart;
    public string? Notes { get; set; }
    public int SortIndex { get; set; }

    public virtual OperationNode OperationNode { get; set; } = null!;
}
