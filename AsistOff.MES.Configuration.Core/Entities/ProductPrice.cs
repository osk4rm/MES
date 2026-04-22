using AsistOff.MES.Configuration.Domain.Constants;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Configuration.Domain.Entities;

public class ProductPrice : IEntity, ISaasy, ISyncable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? SyncId { get; set; }
    public required Guid ProductId { get; set; }
    public required string PriceType { get; set; }
    public required decimal Price { get; set; }
    public string Currency { get; set; } = Currencies.PLN;
    public Guid? MeasureUnitId { get; set; }
    public decimal MinQuantity { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }

    public virtual Product Product { get; set; } = null!;
    public virtual MeasureUnit? MeasureUnit { get; set; }
}