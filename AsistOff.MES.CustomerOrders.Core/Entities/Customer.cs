using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.CustomerOrders.Domain.Entities;

public class Customer : IEntity, ISaasy, ISyncable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? SyncId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? TaxId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ICollection<CustomerOrder> Orders { get; set; } = new List<CustomerOrder>();
}
