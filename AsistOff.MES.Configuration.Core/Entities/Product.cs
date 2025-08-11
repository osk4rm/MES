using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Configuration.Domain.Entities;

public class Product : IEntity, ISaasy, ISyncable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? SyncId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Ean { get; set; }
    public string? Barcode { get; set; }
    public ScanBy ScanBy { get; set; } = ScanBy.Code;
    public bool IsActive { get; set; } = true;
    public Guid? ProductGroupId { get; set; }

    public virtual ProductGroup? ProductGroup { get; set; }
    public virtual ICollection<ProductMeasureUnit> ProductMeasureUnits { get; set; } = new List<ProductMeasureUnit>();
    public virtual ICollection<ProductPrice> ProductPrices { get; set; } = new List<ProductPrice>();
}