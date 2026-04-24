using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Production.Domain.Entities;

/// <summary>
/// An output produced by an operation. Operations may have zero, one, or many
/// outputs (intermediate products, main products, co/by-products, scrap).
/// </summary>
public class OperationOutput : IEntity, ISaasy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid OperationNodeId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? MeasureUnitId { get; set; }
    public decimal Quantity { get; set; }
    public BomQuantityType QuantityType { get; set; } = BomQuantityType.PerUnit;
    public OperationOutputType OutputType { get; set; } = OperationOutputType.MainProduct;
    public Guid? PreferredWarehouseId { get; set; }
    public string? Notes { get; set; }
    public int SortIndex { get; set; }

    public virtual OperationNode OperationNode { get; set; } = null!;
}
