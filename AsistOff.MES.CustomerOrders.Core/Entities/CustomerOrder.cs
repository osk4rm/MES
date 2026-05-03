using AsistOff.MES.CustomerOrders.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.CustomerOrders.Domain.Entities;

public class CustomerOrder : IEntity, ISaasy, ISyncable, IAuditable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? SyncId { get; set; }
    public string? ExternalSystem { get; set; }
    public string? ExternalOrderId { get; set; }
    public required string OrderNumber { get; set; }
    public Guid CustomerId { get; set; }
    public required string CustomerCodeSnapshot { get; set; }
    public required string CustomerNameSnapshot { get; set; }
    public string? CustomerTaxIdSnapshot { get; set; }
    public string? CustomerAddressSnapshot { get; set; }
    public CustomerOrderStatus Status { get; set; } = CustomerOrderStatus.Confirmed;
    public DateTime? OrderDate { get; set; }
    public DateTime? RequestedDeliveryDate { get; set; }
    public DateTime? ConfirmedDeliveryDate { get; set; }
    public string? Currency { get; set; }
    public decimal? TotalNetAmount { get; set; }
    public decimal? TotalGrossAmount { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual Customer Customer { get; set; } = null!;
    public virtual ICollection<CustomerOrderLine> Lines { get; set; } = new List<CustomerOrderLine>();
}
