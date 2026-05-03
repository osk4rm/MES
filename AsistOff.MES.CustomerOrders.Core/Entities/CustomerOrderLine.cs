using AsistOff.MES.CustomerOrders.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.CustomerOrders.Domain.Entities;

public class CustomerOrderLine : IEntity, ISaasy, ISyncable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? SyncId { get; set; }
    public Guid CustomerOrderId { get; set; }
    public string? ExternalLineId { get; set; }
    public int LineNumber { get; set; }
    public Guid ProductId { get; set; }
    public required string ProductCodeSnapshot { get; set; }
    public required string ProductNameSnapshot { get; set; }
    public Guid? MeasureUnitId { get; set; }
    public string? MeasureUnitCodeSnapshot { get; set; }
    public decimal OrderedQuantity { get; set; }
    public decimal ReleasedQuantity { get; set; }
    public DateTime? RequestedDeliveryDate { get; set; }
    public CustomerOrderLineStatus Status { get; set; } = CustomerOrderLineStatus.Open;
    public decimal? UnitNetPrice { get; set; }
    public decimal? LineNetAmount { get; set; }
    public string? Notes { get; set; }

    public decimal RemainingQuantity => Math.Max(OrderedQuantity - ReleasedQuantity, 0);

    public virtual CustomerOrder CustomerOrder { get; set; } = null!;
    public virtual ICollection<CustomerOrderLineProductionRelease> ProductionReleases { get; set; } = new List<CustomerOrderLineProductionRelease>();
}
