using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Configuration.Domain.Entities;

public class ProductGroup : IEntity, ISaasy, ISyncable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? SyncId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? ParentGroupId { get; set; }

    public virtual ProductGroup? ParentGroup { get; set; }
    public virtual ICollection<ProductGroup> ChildGroups { get; set; } = new List<ProductGroup>();
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}